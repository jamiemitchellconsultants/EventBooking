# 06d — Images, release bundle and phase gate (Task 31)

[← Phase overview](phase-6-seed-and-deployment.md) · [Previous task](phase-6c-home-lab.md) · [Ontology](../ontology.md)

This task is the delivery layer of Phase 6. It publishes all five runtime images, attaches the
forward-only migration executable to versioned releases, and carries the decision-bearing phase
pull-request gate.

> Use superpowers:executing-plans. Apply this document after Task 30 on the Phase 6 branch.

**Goal:** Produce traceable GHCR artifacts on `main`, exact-version artifacts on `vX.Y.Z` tags,
and one checksum-covered self-contained EF migrations bundle per release.

**Architecture:** A matrix gives every image the same tag and attestation policy. Main builds get
mutable `latest` plus immutable `sha-xxxxxxx`; tag builds get the exact Git tag only. The release
job waits for every image before creating or updating the matching GitHub release. No credentials
other than the scoped workflow token are introduced.

**Tech Stack:** GitHub Actions, Docker Buildx, GHCR, .NET 10, EF Core 10 and GitHub CLI.

**Spec:** [Master Task 31](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[images and CI/CD](../design/07-deployment.md#images), and
[upgrade and rollback](../design/07-deployment.md#upgrades-and-rollback).

### Task 31: Image and release pipeline plus Phase 6 gate

**Files:**

- Create: .github/workflows/images.yml

**Interfaces:**

```csharp
namespace EventBooking.Deployment;

public sealed record PublishedArtifact(
    string Name,
    string Dockerfile,
    IReadOnlyList<string> Tags,
    string RuntimeUser);

public static class ReleaseContract
{
    // Published for both main and version-tag runs.
    public static readonly string[] Images =
    [
        "eventbooking-api", "eventbooking-mcp", "eventbooking-web",
        "eventbooking-web-caddy", "eventbooking-seed"
    ];

    // Version releases additionally carry this executable and its SHA-256 manifest.
    public const string MigrationBundle = "efbundle-linux-x64";
}
```

- [ ] **Step 1: Write the failing test**

Create this complete workflow. The `dotnet-build.yml` workflow needs no modification: hosted
Ubuntu runners already expose Docker to Testcontainers, and image publication is independent from
the build job's path-filter behaviour.

```yaml
# .github/workflows/images.yml (complete)
name: Publish images and release bundle

on:
  push:
    branches: [main]
    tags: ["v*.*.*"]

permissions:
  contents: write
  packages: write

concurrency:
  group: images-${{ github.ref }}
  cancel-in-progress: false

env:
  REGISTRY: ghcr.io
  IMAGE_NAMESPACE: ghcr.io/jamiemitchellconsultants
  DOCKER_METADATA_SHORT_SHA_LENGTH: 7

jobs:
  images:
    name: ${{ matrix.name }}
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        include:
          - name: eventbooking-api
            dockerfile: src/EventBooking.Api/Dockerfile
          - name: eventbooking-mcp
            dockerfile: src/EventBooking.Mcp/Dockerfile
          - name: eventbooking-web
            dockerfile: src/EventBooking.Web/Dockerfile
          - name: eventbooking-web-caddy
            dockerfile: src/EventBooking.Web/Dockerfile.caddy
          - name: eventbooking-seed
            dockerfile: src/EventBooking.SeedData/Dockerfile
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4.4.0

      - uses: docker/setup-buildx-action@8d2750c68a42422c14e847fe6c8ac0403b4cbd6f # v3.12.0

      - uses: docker/login-action@c94ce9fb468520275223c153574b00df6fe4bcc9 # v3.7.0
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - id: metadata
        uses: docker/metadata-action@c299e40c65443455700f0fdfc63efafe5b349051 # v5.10.0
        with:
          images: ${{ env.IMAGE_NAMESPACE }}/${{ matrix.name }}
          tags: |
            type=raw,value=latest,enable=${{ github.ref == 'refs/heads/main' }}
            type=sha,prefix=sha-,format=short,enable=${{ github.ref == 'refs/heads/main' }}
            type=ref,event=tag

      - uses: docker/build-push-action@10e90e3645eae34f1e60eeb005ba3a3d33f178e8 # v6.19.2
        with:
          context: .
          file: ${{ matrix.dockerfile }}
          push: true
          platforms: linux/amd64
          tags: ${{ steps.metadata.outputs.tags }}
          labels: ${{ steps.metadata.outputs.labels }}
          cache-from: type=gha,scope=${{ matrix.name }}
          cache-to: type=gha,mode=max,scope=${{ matrix.name }}
          provenance: mode=max
          sbom: true

  release-bundle:
    if: startsWith(github.ref, 'refs/tags/v')
    needs: images
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4.4.0

      - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4.3.1
        with:
          dotnet-version: "10.0.x"

      - name: Build self-contained migration bundle
        run: |
          dotnet restore EventBooking.sln
          dotnet tool install --global dotnet-ef --version 10.0.0
          mkdir -p artifacts
          dotnet ef migrations bundle \
            --project src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj \
            --startup-project src/EventBooking.SeedData/EventBooking.SeedData.csproj \
            --configuration Release \
            --self-contained \
            --target-runtime linux-x64 \
            --output artifacts/efbundle-linux-x64
          (cd artifacts && sha256sum efbundle-linux-x64 > efbundle-linux-x64.sha256)

      - name: Create or update matching release
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          if gh release view "$GITHUB_REF_NAME" >/dev/null 2>&1; then
            gh release upload "$GITHUB_REF_NAME" artifacts/* --clobber
          else
            gh release create "$GITHUB_REF_NAME" artifacts/* --verify-tag --generate-notes
          fi
```

Run actionlint immediately. Expected: clean; a parser failure means the workflow is not complete.

```bash
actionlint .github/workflows/images.yml .github/workflows/compose-smoke.yml
```

- [ ] **Step 2: Dry-build all five final images and prove non-root runtime users**

```bash
set -euo pipefail
for spec in \
  eventbooking-api:src/EventBooking.Api/Dockerfile \
  eventbooking-mcp:src/EventBooking.Mcp/Dockerfile \
  eventbooking-web:src/EventBooking.Web/Dockerfile \
  eventbooking-web-caddy:src/EventBooking.Web/Dockerfile.caddy \
  eventbooking-seed:src/EventBooking.SeedData/Dockerfile
do
  image="${spec%%:*}"
  dockerfile="${spec#*:}"
  docker build --file "$dockerfile" --tag "$image:task31" .
  user="$(docker image inspect "$image:task31" --format '{{.Config.User}}')"
  printf '%s\t%s\n' "$image" "$user"
  test -n "$user"
  test "$user" != 0
  test "$user" != root
done
```

Build a local migration bundle with the same command as CI and execute its help path so a missing
runtime dependency is visible without applying a migration:

```bash
if ! dotnet ef --version >/dev/null 2>&1; then
  dotnet tool install --global dotnet-ef --version 10.0.0
fi
mkdir -p artifacts/task31
dotnet ef migrations bundle \
  --project src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj \
  --startup-project src/EventBooking.SeedData/EventBooking.SeedData.csproj \
  --configuration Release --self-contained --target-runtime linux-x64 \
  --output artifacts/task31/efbundle-linux-x64
artifacts/task31/efbundle-linux-x64 --help
sha256sum artifacts/task31/efbundle-linux-x64
```

If the repository does not carry a local EF tool manifest, install exactly the same 10.0.0 tool
version used by CI before rerunning; do not replace the workflow with an unpinned latest install.

- [ ] **Step 3: Run the Phase 6 verification gate**

```bash
docker compose config --quiet
docker compose up --detach --build --wait postgres keycloak mailpit
docker compose --profile seed run --rm seed --demo --reanchor
docker compose up --detach --build --wait api mcp web
curl --fail http://localhost:5001/health/ready
curl --fail http://localhost:5001/api
test "$(curl --fail --silent http://localhost:8025/api/v1/messages | jq '.messages | length')" -ge 1
docker compose down --volumes --remove-orphans

cd deploy/home-lab
shellcheck install.sh ../postgres/init-roles.sh
jq --exit-status . keycloak/eventbooking-realm.json >/dev/null
docker compose --env-file .env.example config --quiet
cd ../..

dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
node scripts/build-ontology.mjs --check
```

Expected: every command passes and no test is skipped. Add the executor's actual project counts and
home-lab rehearsal record to HANDOVER.md. No number from Task 11 or this plan may be copied forward
as though Phase 6 had measured it.

- [ ] **Step 4: Commit and push**

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "ci: publish images and migrations bundle"
git push
```

After the push, compute the fingerprint from the pushed head and draft the complete body:

```bash
merge_base="$(git merge-base origin/main HEAD)"
fingerprint="$(git diff "$merge_base" HEAD | sha256sum | cut -c1-12)"
body_file="$(mktemp)"
cat >"$body_file" <<EOF
## Summary

- make database migration the safe seed default and keep demo data explicit
- add the generalised six-type demo graph, including managed LAB coverage
- add local and home-lab Compose deployments with smoke and recovery procedures
- publish five non-root images and a checksum-covered migrations bundle

## Narrative Context

EventBooking needed reproducible demo data and two supported container deployment shapes after the
application and Web surfaces were defined.

## Narrative Decision

Use a migrate-only seed default, add LAB as the fourth managed active appointment type, retire the
single-zone clock, and support local Compose plus an isolated home-lab topology behind shared Caddy
and Keycloak services.

## Narrative Consequences

Demo state now requires an explicit flag, destructive reseeding has a second guard, operators have
documented forward-only upgrade and fresh-volume recovery paths, and releases publish five images
plus a self-contained migration executable.

AI-Fingerprint: sha256:$fingerprint
EOF
node scripts/check-ontology-terms.mjs --also "$body_file"
cat "$body_file"
```

STOP AND ASK THE USER FOR APPROVAL before opening the pull request. After approval only:

```bash
gh pr create \
  --base main \
  --head codex/phase-6-seed-and-deployment \
  --title "Seed and deploy EventBooking" \
  --body-file "$body_file" \
  --label narrative-required
```

Do not merge. Wait for the pull request to report the pushed head; if any later commit changes
HEAD, recompute the fingerprint and update the body before treating CI as authoritative.
