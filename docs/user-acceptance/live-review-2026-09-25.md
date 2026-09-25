# Live review — 25 September 2026

[← Acceptance pack](README.md)

**Deployment:** [EventBooking home-lab](https://eventbooking.tqaentry.com/)

**Method:** anonymous browser visit after the reported redeployment, plus read-only inspection of the public API and deployed assets. No staff credentials or valid attendee links were available during this review. A page can take several seconds to finish loading; the observations below were made after its final content appeared.

| Case | Observed result | Assessment |
| --- | --- | --- |
| UAT-00, home page | The landing page shows **Sign in** and **Read the attendee guide**. An **“An unhandled error has occurred.”** strip remains visible, and much of the page appears unstyled. | Fail: the visible error strip and presentation do not meet the smoke expectation. |
| UAT-00, public Help | `/help` shows **Something went wrong. Please try again.** and **Try again** instead of the attendee guide. The Markdown asset at `/help/attendee.md` is publicly reachable. | Fail: anonymous Help is unusable. |
| UAT-00, sign-in handoff | **Sign in** opens the EventBooking identity-provider login page with username and password fields. | Pass for the handoff only; return to the staff home page was not tested. |
| ATT-03, invalid invitation | A deliberately invalid test link at `/book/invalid-uat-token` shows **This link has expired.** and the configured Coordinator contact. | Pass for the invalid-link message only. |
| ATT-03, invalid management link | A deliberately invalid test link at `/manage/invalid-uat-token` shows **This link has expired** and **This booking link is no longer valid.** | Pass for the invalid-link message only. |

The deployed `/theme.css` contains only three `--eventbooking-*` variables, while the app's CSS uses variables such as `--font-body` and `--color-primary`. The browser computed the body font as Times, consistent with missing theme tokens. The repository's web theme defines the larger token set. The error strip also has no hiding rule in the deployed CSS, so its visibility alone does not prove the page stopped running. The Help failure is a separate observed behaviour; its cause was not established by this browser review.

**Still to review against live accounts:** ADM-01–05, MGR-01–04, COO-01–07, APS-01–03, ATT-01–02, and the signed-in part of UAT-00. Those cases remain scripts to execute, not passing results.
