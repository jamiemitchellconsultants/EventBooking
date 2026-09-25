using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireTransitionalSeedRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The predecessor's fixed reference rows are retired only where nothing uses them. A
            // database that has built real data on them keeps each row that data still names,
            // rather than failing the upgrade on a Restrict foreign key or, for the location,
            // which no foreign key covers, orphaning every event recorded there. "Uses" means
            // any uuid column in the schema holding the id, so a later column is covered without
            // editing this list. Groups go first, each with its own requirements, so a retired
            // group's requirements stop pinning the types they name.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    retired record;
                    referencing record;
                    referenced boolean;
                BEGIN
                    FOR retired IN
                        SELECT * FROM (VALUES
                            (1, 'attendee_group', 'e0000001-0000-0000-0000-000000000001'::uuid),
                            (1, 'attendee_group', 'e0000002-0000-0000-0000-000000000002'::uuid),
                            (1, 'attendee_group', 'e0000003-0000-0000-0000-000000000003'::uuid),
                            (1, 'attendee_group', 'e0000004-0000-0000-0000-000000000004'::uuid),
                            (1, 'attendee_group', 'e0000005-0000-0000-0000-000000000005'::uuid),
                            (2, 'appointment_type', 'a0000001-0000-0000-0000-000000000001'::uuid),
                            (2, 'appointment_type', 'a0000002-0000-0000-0000-000000000002'::uuid),
                            (2, 'appointment_type', 'a0000003-0000-0000-0000-000000000003'::uuid),
                            (2, 'location', '10000000-0000-0000-0000-000000000001'::uuid)
                        ) AS rows (pass, table_name, id)
                        ORDER BY pass, table_name, id
                    LOOP
                        referenced := false;
                        FOR referencing IN
                            SELECT c.table_name, c.column_name
                              FROM information_schema.columns c
                              JOIN information_schema.tables t
                                ON t.table_schema = c.table_schema AND t.table_name = c.table_name
                             WHERE c.table_schema = 'public'
                               AND t.table_type = 'BASE TABLE'
                               AND c.data_type = 'uuid'
                               AND c.column_name <> 'id'
                               AND NOT (retired.table_name = 'attendee_group'
                                   AND c.table_name = 'attendee_group_requirement'
                                   AND c.column_name = 'attendee_group_id')
                        LOOP
                            EXECUTE format('SELECT EXISTS (SELECT 1 FROM %I WHERE %I = $1)',
                                    referencing.table_name, referencing.column_name)
                                INTO referenced USING retired.id;
                            EXIT WHEN referenced;
                        END LOOP;

                        IF referenced THEN
                            RAISE NOTICE 'Kept % % because existing data still references it.',
                                retired.table_name, retired.id;
                        ELSE
                            IF retired.table_name = 'attendee_group' THEN
                                DELETE FROM attendee_group_requirement WHERE attendee_group_id = retired.id;
                            END IF;
                            EXECUTE format('DELETE FROM %I WHERE id = $1', retired.table_name)
                                USING retired.id;
                        END IF;
                    END LOOP;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores only what Up removed: a kept row, or a newer row now holding the code, is
            // left alone, and a requirement comes back only when both of its rows exist.
            migrationBuilder.Sql(
                """
                INSERT INTO appointment_type (id, code, is_active, name, version) VALUES
                    ('a0000001-0000-0000-0000-000000000001', 'DAT', true, 'Drug & Alcohol Testing', 1),
                    ('a0000002-0000-0000-0000-000000000002', 'MED', true, 'Medical Check-up', 1),
                    ('a0000003-0000-0000-0000-000000000003', 'UNI', true, 'Uniform Fitting', 1)
                ON CONFLICT DO NOTHING;

                INSERT INTO attendee_group (id, code, is_active, name, version) VALUES
                    ('e0000001-0000-0000-0000-000000000001', 'CABIN_CREW', true, 'Cabin Crew', 1),
                    ('e0000002-0000-0000-0000-000000000002', 'PILOTS', true, 'Pilots', 1),
                    ('e0000003-0000-0000-0000-000000000003', 'GROUND_OPERATIONS_AGENT', true, 'Ground Operations Agent', 1),
                    ('e0000004-0000-0000-0000-000000000004', 'ENGINEERING', true, 'Engineering', 1),
                    ('e0000005-0000-0000-0000-000000000005', 'GROUND_TRANSPORT_SERVICES', true, 'Ground Transport Services', 1)
                ON CONFLICT DO NOTHING;

                INSERT INTO location (id, address, code, is_active, name, time_zone_id, version) VALUES
                    ('10000000-0000-0000-0000-000000000001', 'Recorded against the transitional site until Phase 3.',
                        'TRANSITIONAL', true, 'Transitional location', 'Europe/London', 1)
                ON CONFLICT DO NOTHING;

                INSERT INTO attendee_group_requirement (appointment_type_id, attendee_group_id)
                SELECT pairs.appointment_type_id, pairs.attendee_group_id
                  FROM (VALUES
                      ('a0000001-0000-0000-0000-000000000001'::uuid, 'e0000001-0000-0000-0000-000000000001'::uuid),
                      ('a0000002-0000-0000-0000-000000000002'::uuid, 'e0000001-0000-0000-0000-000000000001'::uuid),
                      ('a0000003-0000-0000-0000-000000000003'::uuid, 'e0000001-0000-0000-0000-000000000001'::uuid),
                      ('a0000001-0000-0000-0000-000000000001'::uuid, 'e0000002-0000-0000-0000-000000000002'::uuid),
                      ('a0000003-0000-0000-0000-000000000003'::uuid, 'e0000002-0000-0000-0000-000000000002'::uuid),
                      ('a0000002-0000-0000-0000-000000000002'::uuid, 'e0000003-0000-0000-0000-000000000003'::uuid),
                      ('a0000002-0000-0000-0000-000000000002'::uuid, 'e0000004-0000-0000-0000-000000000004'::uuid),
                      ('a0000001-0000-0000-0000-000000000001'::uuid, 'e0000005-0000-0000-0000-000000000005'::uuid),
                      ('a0000002-0000-0000-0000-000000000002'::uuid, 'e0000005-0000-0000-0000-000000000005'::uuid),
                      ('a0000003-0000-0000-0000-000000000003'::uuid, 'e0000005-0000-0000-0000-000000000005'::uuid)
                  ) AS pairs (appointment_type_id, attendee_group_id)
                 WHERE EXISTS (SELECT 1 FROM appointment_type WHERE id = pairs.appointment_type_id)
                   AND EXISTS (SELECT 1 FROM attendee_group WHERE id = pairs.attendee_group_id)
                ON CONFLICT DO NOTHING;
                """);
        }
    }
}
