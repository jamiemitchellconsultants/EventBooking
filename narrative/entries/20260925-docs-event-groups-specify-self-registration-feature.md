---
date: 2026-09-25
slug: docs-event-groups-specify-self-registration-feature
title: "docs(event-groups): specify self-registration feature"
summary: "Introduce Event Groups with selected Attendee Groups and independent publication gates for each Event membership."
kind: product
status: accepted
sequence: 2026-09-25T19:02:59.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/74; merge commit 64c09664473d4d1f39a8c4d88d8c049fdf6f6689"
---

## Context

The current workflow requires staff to create Attendees. The requested feature lets Admins and Coordinators publish selected Events for potential attendees while preserving Event capacity and role boundaries.

## Decision

Introduce Event Groups with selected Attendee Groups and independent publication gates for each Event membership. Interpret appointment-type compatibility as exact equality between each member Event's types and the union of selected Attendee Group requirements. Collect name, email, and group on the public form, then require email confirmation before creating a Booking or charging capacity. At confirmation, use the existing Attendee, Invite, Booking, and outbox pipeline.

## Consequences

A pending registration does not reserve capacity, so confirmation can fail if a gate closes or capacity fills. The exact-union interpretation is a stated planning assumption for review. The implementation is divided into nine separately committable tasks; no feature code is included here.

AI-Fingerprint: sha256:9e29ab77a07f
