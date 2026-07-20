# Monetization implementation plan (Phase 3)

Agreed 2026-07-20. **Not started.** Written so the whole thing can be executed in one pass —
the pieces share compliance work, and doing them piecemeal means doing that work three times.

**Sequencing decision: this comes AFTER play-testing and the store listing.** Ad placement
depends on where players actually stall and how long levels really take, and installs (not
ad configuration) are what determine whether any of this earns anything.

---

## 0. The decision, and why

**Google AdMob**, single network, no mediation to start.

| Reason | Detail |
| --- | --- |
| No traffic minimum | Serves from the first install; mediation platforms effectively want existing volume |
| Consent SDK bundled | Google's **UMP** ships inside the Mobile Ads Unity plugin and is IAB-TCF certified. A non-Google network means sourcing a separate certified CMP — work on the most compliance-sensitive part of the feature |
| Families-safe | AdMob is a Families Self-Certified Ads SDK. Mediation adapters mostly are not, and **that programme is not accepting new applicants** — adding networks could permanently close the child-audience option |
| One account, one SDK | Solo-dev overhead matters |

**Revisit mediation** (AppLovin MAX / Unity LevelPlay) only when daily actives make a
percentage uplift worth real money. AdMob supports adding mediation later without a rewrite.

---

## 1. Placements

| Format | Placement | Notes |
| --- | --- | --- |
| **Rewarded** | "Double your pearls" on the win screen | The best earner and the safest. Pearls buy only aquarium decor, so this cannot touch the fairness boundary |
| **Rewarded** | Free daily pearl bundle, once per day | Pairs with the Daily Reef; a reason to open the app |
| **Interstitial** | Every 3rd–4th level *completion* | **Never** after a first-time loss, never mid-level, never on the first session |
| **Banner** | — | Rejected. A 13-column portrait board can't spare the pixels for the lowest-value format |
| **Rewarded "continue"** | ⚠️ **UNDECIDED — needs an explicit call** | Highest-revenue placement in most puzzle games, but it grants extra shots, i.e. it *changes the outcome of a level*. The whole codebase treats the fairness boundary as non-negotiable. Decide deliberately; do not let it slip in |

## 2. IAP

- **Remove Ads**, ~£2–3, non-consumable. Disables interstitials. **Keep rewarded ads
  available** — they're opt-in and players still want the pearls.
- **Restore Purchases** button in Settings — Play requires entitlement restoration.
- Pearl packs: later, only if there's traction.

---

## 3. Implementation order

1. **Google Mobile Ads Unity plugin** (latest — see §5 on staleness).
2. **`Ads.cs`** service beside `Sfx.cs`: null-safe static singleton, same pattern as
   `PopEffects`/`Sfx`, created in `GameBootstrap`. All call sites must survive it being
   absent so the editor and any no-network path still work.
3. **UMP consent FIRST, before any ad loads.** Gather on boot; store nothing yourself.
4. **Rewarded pearls** — hook the GameFlow win block, next to the existing pearl award.
   Award through `Pearls.Add` so the economy stays in one place.
5. **Interstitial pacing** — a counter in GameFlow's level-complete path. Persist it, so
   quitting between levels can't be used to skip the count.
6. **Remove-ads IAP** + restore.
7. **Settings page**: "Remove Ads" (or "Ads removed ✓"), "Restore Purchases",
   "Privacy options" → re-open the UMP form. All three are required or expected.
8. **Rewrite the privacy text and the Data safety form together** (§4).

### Architectural constraints — do not break

- **Fairness boundary.** Ads are presentation. Nothing in an ad callback may touch
  aim/attach/match, and no ad may run while a shot is resolving.
- **Pause and audio.** An ad takes focus: pause the game and duck/stop music on
  `OnAdShowed`, restore on close. `Sfx.SetMusic` already fades, so reuse it.
- **No ad on a cold start** before the player has played once.

---

## 4. Compliance — the part that bites

**This undoes the current "no data collected" position, which is presently truthful.**
The in-app Privacy page (`GameFlow.PrivacyText`) and `docs/privacy-policy.md` both currently
assert *no network, no ads, no analytics*. **Both must change in the same commit as the SDK.**

- **Data safety** must then declare, per Google's own AdMob disclosure page:
  Device or other IDs · App interactions · Diagnostics · **Approximate location**
  (the commonly-missed one — the SDK derives coarse location from IP), all as
  **collected AND shared**, purposes Advertising / Analytics / Fraud prevention.
- **`AD_ID` permission** is merged in automatically by the SDK on API 33+. Your manifest will
  contain it whether you wrote it or not, and Data safety must match — mismatch is a very
  common rejection.
- **`QUERY_ALL_PACKAGES`**: no qualifying use here. If an SDK merges it in, strip it with
  `tools:node="remove"` and check the merged manifest.
- **EEA/UK**: consent must be gathered *before* loading ads and be **revocable at any time**
  (hence the Settings entry). Configure and publish the message in
  **AdMob ▸ Privacy & messaging** — dropping in the SDK alone does nothing.
- **Play Console**: declare that the app contains ads. Misrepresenting is suspension-level.
- **Target audience**: settle this *before* integrating (see `PLAY_STORE_CHECKLIST.md`).

---

## 5. Gotchas

- **Use Google's TEST ad unit IDs throughout development.** Clicking your own live ads is a
  policy violation and gets accounts banned. Register your phone as a test device too.
- **Check the Mobile Ads SDK's own minSdk.** We set 26 because Unity demanded it; Google's
  SDK raises its floor independently and may force it higher. *(The earlier research checked
  Play policy, not what the tooling itself accepts — that is how the minSdk-25 error got
  through.)*
- **Keep the plugin current.** TCF v2.3 strings became mandatory in March 2026. A stale
  plugin means non-compliant consent, which silently downgrades you to non-personalised
  ads — lost revenue with no error message.
- **Test with no network** and with consent *declined*: both must leave the game playable.
