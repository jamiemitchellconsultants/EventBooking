# Live review after UI fixes — 25 September 2026

[← Acceptance pack](README.md) · [Earlier live review](live-review-2026-09-25.md)

**Deployment:** [EventBooking home-lab](https://eventbooking.tqaentry.com/) after the reported UI redeployment.

**Method:** signed-out browser visit, visual inspection of the rendered page, and comparison of the acceptance scripts with the latest `origin/main` page code at `d0411d7`. No staff credentials or valid attendee links were available. The browser was allowed to finish loading before each observation. The deployment's exact commit could not be established from the UI.

| Case | Observed result | Assessment |
| --- | --- | --- |
| UAT-00, home page | **Sign in** and **Read the attendee guide** appear on a styled landing page. The previously visible error strip is absent from the rendered view. | Pass for the signed-out landing page. The error-strip markup remains in the page tree, but is not visible. |
| UAT-00, public Help | **Read the attendee guide** opens `/help`, which still shows **Something went wrong. Please try again.** and **Try again**. | Fail: the attendee guide is still unavailable through Help. |
| UAT-00, sign-in handoff | **Sign in** opens the EventBooking identity-provider page with username and password fields. | Pass for the handoff only; return to the staff home page was not tested. |
| ATT-03, invalid invitation | A deliberately invalid test link at `/book/invalid-uat-token` shows **This link has expired.** and a Coordinator contact. | Pass for the invalid-link message only. |
| ATT-03, invalid management link | A deliberately invalid test link at `/manage/invalid-uat-token` shows **This link has expired** and **This booking link is no longer valid.** | Pass for the invalid-link message only. |

The latest source groups staff home links into **Your work**, **Administration** for Admin, **Reference data** for Manager and Coordinator, and **Help**. The discovery steps in ADM-01, MGR-01, COO-01, and APS-01 now name those sections. These signed-in sections were checked against source, not observed in a staff session.

**Still to review with live test accounts and links:** the signed-in part of UAT-00, ADM-01–05, MGR-01–04, COO-01–07, APS-01–03, and ATT-01–02. ATT-03's mobile and accessibility checks also remain to run. These are executable scripts, not passing results.
