# Air Footy playtest protocol

**Maintainer:** JD<br>
**Revised:** 1 August 2026

I use this sheet for repeatable checks after gameplay, prefab or integration changes. I do not mark a case as passed from a compile alone.

## Head-to-head pass

- Start Blue and Red perspectives separately.
- Confirm countdown disables movement, pulse, dash, AI and ball motion.
- Confirm each striker stays in its own half and slides around curved rails without snagging.
- Test a passive save, tap pulse, full pulse, dash strike, turbo pulse and turbo dash.
- Confirm charge pips spend and recover in order.
- Leave the ball slow and unattended; one re-drop should occur after the configured grace.
- Place the ball near a striker; the longer possession grace should prevent a premature re-drop.
- Verify AI near-post, far-post and bank attempts, visible charge telegraph, reactive save and recovery.
- Score into both goals and finish a first-to-five match.

## Four-team pass

- Start once as Blue, Red, Green and Gold.
- Confirm the selected team is human-controlled and every other active team is AI-controlled.
- Confirm both balls start, stop, reset and re-drop independently.
- Confirm team semicircle limits prevent centre camping while still allowing defensive movement.
- Concede five goals into each side and verify only that side is eliminated.
- Confirm eliminated goals and strikers no longer affect play.
- Continue until one team remains and verify the winner, local score and return flow.

## Feedback pass

- Compare passive, pulse, perfect/turbo, wall and goal impacts; each should read differently without hiding the ball.
- Check camera response from every selectable team perspective.
- Check charge, dash aim, AI telegraph, ball trail, hover ring, rally glow, score UI, crowd and jumbotron response.
- Pause during a held action and during goal reset; no input should fire after resume unless pressed again.

## Integration pass

- Launch from the Air Footy menu and from its arcade cabinet.
- Quit through the pause menu and finish a match normally.
- Confirm score, best score, tickets and completion unlock update through `PlayerScoreCarrier`.
- Confirm standalone play logs one concise persistence notice when no carrier exists.
- Confirm the correct hub or menu scene loads after the configured delay.

## Record

| Date | Build/commit | Mode/team | Result | Problem found | Follow-up |
|---|---|---|---|---|---|
| 1 Aug 2026 | Documentation and prefab-bake revision | Automated compile/import | Pending hands-on pass | No manual result claimed | Run both mode passes in Play Mode |
