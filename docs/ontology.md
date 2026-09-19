# EventBooking — Application Ontology

<!-- GENERATED FROM ontology.ttl. Edit that file, not this one. -->

> **AI instructions:** Read this file in full before starting any task that touches domain
> concepts. To change it, edit `ontology.ttl` and run `node scripts/build-ontology.mjs`. This
> file must never lag behind the code. See the Ontology protocol section in this repository's
> agent-instruction file.

Every domain concept is named here exactly once, as a backticked PascalCase term. Code, specs,
plans and prose use those names; `ontology.ttl` is the source, this file and they are the
consumers. A term that appears in Markdown but not here fails the deterministic check in
`scripts/check-ontology-terms.mjs`.

**Delete the sections that do not apply to this repository from `ontology.ttl`**, and remove the
matching names from `sections` in `ontology.config.json`. An empty section is worse than an
absent one — the optional semantic reviewer treats an empty required section as a configuration
error.
---

## Entities

| Name | Properties | Description |
| --- | --- | --- |
| `Event` |  | A bookable occurrence that offers one or more `SessionType`s. Properties are undecided until the first code lands. |
| `SessionType` |  | A kind of session an `Event` can offer. Whether this is modelled as an entity or an enumeration is undecided until the first code lands. |

---

## Relationships

| From | Relationship | To | Cardinality |
| --- | --- | --- | --- |
| `Event` | offers | `SessionType` | 1 → 1..* |
