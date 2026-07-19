# Coral Cascade — Google Play publishing checklist

Status as of 2026-07-19. Requirements verified against official Google sources on that date;
**re-check anything dated before you submit**, Play deadlines move.

---

## 1. Done in the project (committed)

| Item | Value | Notes |
| --- | --- | --- |
| Build scene | `Phase1Prototype.unity` | **Was `SampleScene` — a build would have launched blank.** SampleScene is still listed but disabled. |
| Application ID | `com.jhong03.coralcascade` | ⚠️ **PERMANENT once published.** Change it now if you want something else. |
| Company / product | `jhong03` / `Coral Cascade` | was `DefaultCompany` |
| Scripting backend | IL2CPP | required for ARM64 |
| Architecture | ARM64 | Play requires a 64-bit binary; Unity ships native code, so this binds |
| Target SDK | **36** | API 35 is the floor today; **36 becomes mandatory 2026-08-31** — shipping 36 now avoids a redo |
| Min SDK | 25 | no Play-enforced floor; ⚠️ check the Google Mobile Ads SDK's own minimum when you add ads |
| Orientation | portrait only | applied by the editor script |
| Safe area | `renderOutsideSafeArea = true` + UI insets | pairs with the `SafeAreaUtil` work |
| App icon | `Assets/Art/Icon/` | generated; adaptive foreground/background + legacy/round |
| Privacy policy (in-app) | Settings ▸ Privacy | Play accepts a link **or text** in-app; this is the text half |

### One manual step

Icons can't be assigned safely by hand-editing YAML, so:

> **Unity ▸ Coral Cascade ▸ Play Store ▸ Apply Release Settings**

then **Verify Release Settings** to print what's still missing. Both are re-runnable.

---

## 2. Still to do — blocking

- [ ] **INSTALL ANDROID BUILD SUPPORT.** Discovered 2026-07-20: this editor has only
      WebGL and Windows player modules, so **no APK/AAB can be built at all**, and launcher
      icons cannot be assigned (which is why Verify reported a confusing "no launcher icon").
      The Android *settings* serialise fine without the module, so the inspector looks
      configured — nothing warns you until you try to build.
      > Unity Hub ▸ Installs ▸ 6000.5.3f1 ▸ ⚙ ▸ Add modules ▸ **Android Build Support**,
      > including **OpenJDK** and **Android SDK & NDK Tools**.
      Then re-run **Apply Release Settings** so the icons land.

- [ ] **Keystore.** Not created deliberately: it needs a password that must not live in the
      repo. Create an upload key, enable Play App Signing, and **back the keystore up** —
      losing it means you can never update the app.
- [ ] **Hosted privacy policy URL** for the Play Console field. Must be a live, publicly
      accessible, **non-geofenced, non-PDF, non-editable** page (a Google Doc will be
      rejected) naming the same entity as the listing. The in-app text is already written —
      host the same content.
- [ ] **Data safety form.** Mandatory for every published app, *including apps that collect
      nothing*. Right now the honest answer is "no data collected". **This changes the day
      ads or analytics land** — see §4.
- [ ] **Content rating questionnaire (IARC).** Apps without a rating get removed.
- [ ] **Target audience declaration.** ⚠️ The highest-leverage choice here. Declaring
      children in the audience pulls in the whole Families policy (certified ads SDKs only,
      no interest-based ads, no advertising ID at all for child-only apps). Declaring 13+
      avoids that — but Google reviews the declaration and can reclassify, and a bright
      cartoon fish game is exactly the profile that gets a second look.
- [ ] **App access.** Declare "all functionality available without special access" — leaving
      it blank blocks review.
- [ ] **"We have none of these" forms** — Financial features, Health, Government. All three
      must be *actively* completed with a negative declaration.
- [ ] **Closed testing gate (personal accounts).** Accounts created after 2023-11-13 need
      **12 testers opted in for 14 continuous days** before production access, then ~7 days
      review. **Organisation accounts are exempt** — worth deciding before you register.
      Budget 3+ weeks.

Not applicable: **account deletion** (no accounts — don't over-declare it).

---

## 3. Still to do — product

- [ ] **Store listing**: feature graphic (1024×500), phone screenshots, short/full
      description. The generated `icon_full.png` doubles as the 512×512 store icon.
- [ ] Play through all 70 levels, or at least spot-check the new waves.
- [ ] Test on a real device — everything below is compile-verified only.

---

## 4. When ads / IAP land (Phase 3)

- **Certified CMP is mandatory** for EEA/UK personalised ads (since 2024-01-16; Switzerland
  2024-07-31). Google's **UMP SDK** ships inside the Unity Mobile Ads plugin and is
  IAB-certified for **TCF v2.3** — but the SDK alone does nothing: you must configure and
  publish the message in **AdMob ▸ Privacy & messaging**. Consent must be gathered *before*
  loading ads, and must be revocable — put that entry point in the Settings page next to
  Privacy.
- **Data safety must then declare** (per Google's own AdMob disclosure page): Device or
  other IDs, App interactions, Diagnostics, **and Approximate location** — the last is the
  commonly-missed one, because the SDK derives coarse location from IP. All as
  *collected AND shared*.
- **`AD_ID` permission** is merged in automatically by ad SDKs on API 33+. Your manifest
  will contain it whether you wrote it or not, and the Data safety form must match — a
  mismatch is a very common rejection.
- **`QUERY_ALL_PACKAGES`**: a bubble shooter has no qualifying use. If an SDK merges it in,
  strip it with `tools:node="remove"` and check the merged manifest.
- **Restore purchases** button in Settings for the remove-ads entitlement.
- Merchant account for IAP publishes your **full legal address** on Play.

---

## 5. Diary dates

| Date | What |
| --- | --- |
| **2026-08-31** | API 36 required for new apps and updates (extension to 2026-11-01 on request) |
| **2026-09-30** | Play package registration deadline — most apps were auto-registered in March 2026, **verify yours** |

Lower-confidence, flagged by the research: Utah age-verification timing, the exact Firebase
Analytics data-type list, and whether EU DSA trader status applies to Play at all (it could
not be confirmed to exist — check the live Console at submission).
