# Basic rules engine

This is a playable reconstruction, not a verified clone of every server-side rule. No character stats, equipment, special skills, paid dice bonuses, teams, or original-game account system are simulated. Steam lobby and multiplayer transport are implemented separately in Marble/Online.

The current renderer uses independently authored procedural artwork and does not load extracted visual resources. See `Documentation/original-art.md`.

## Evidence adopted

The board uses the 32 names/order, eight city groups and five resort locations extracted from APK `RgnInfo0.dat` and `ADD_ALL`. See `Documentation/apk/findings.md` and `C:/Users/ksh00/project/MarbleResearchArchive/removed-game-art-20261025/Assets/StreamingAssets/original-map.json` for source offsets, raw values and limitations. Map0 has a bonus game and no tax tile.

Packaged guide labels establish: built color sets double toll; three completed color sets, a whole line, or every resort win; island escape can use a double, payment or a card; landmarks prevent takeover; start allows owned-city additional construction; world-travel destination is selected next turn; bonus-game success returns double the entry fee; emergency loans and selling property address cash shortage. Blue-resort owned-count and pink-resort arrival rules are described in labels. Mapping raw resort types to those descriptions remains an inference.

## Implemented behavior

- Two to four equal players, with any count of humans from zero to the player count. All use the same mechanics. The presentation decides when CPU actions execute.
- A 32-space clockwise loop. Two independent dice, values 1–6. A double grants another roll after landing actions; a third consecutive double sends the player to the island and ends the turn. Island escape doubles do not grant another roll.
- Crossing start pays one salary per complete lap and unlocks later building stages. Landing start enables one remote construction on an owned city. No extra salary is added solely because start was the exact destination.
- Unowned land can be purchased. Villa, building and hotel are independent one-per-type improvements represented by a bit mask. Land and selected improvements can be purchased atomically. Existing improvement types cannot be repurchased. One construction operation per landing; all three types and a later visit are needed for the landmark. Building and hotel unlock after one and two laps. Any subset of unlocked, missing ordinary types can be bought together; unselected lower types are not added automatically. The construction dialog shows the combined price and projected toll.
- Opponent toll is paid before a takeover offer. Taking over transfers twice invested cost to the seller; landmarks cannot be taken over. A complete city color requires every city owned and at least villa level. Its toll is doubled.
- Blue resort toll doubles as the owner acquires additional blue resorts. Pink resort toll doubles on an owned-resort arrival, including the owner's, at most three times. Tourism victory requires all five resorts of both types.
- Island landing or a jail card sends the player there. Up to three failed turns, optional escape payment/card, and mandatory payment plus movement after the third unsuccessful roll.
- World-travel landing ends the turn; destination selection replaces the next roll. Movement is clockwise and can receive salary; the destination resolves normally. Travel can be skipped.
- Olympics selects an owned property and raises its persistent toll multiplier by one, capped at five. A player without land can skip.
- Bonus game is optional heads/tails, entry fee 100, winning payout 200. It is separate from the dice RNG while using the same deterministic random stream.
- Ten reconstructed fortune outcomes: cash award, repair fee, start move, island move, escape card, toll shield, salary-sized award, world travel, nearest resort, or three-space advance. A held toll shield is used automatically on the next opponent toll.
- Insufficient compulsory payments enter an explicit debt state. The player may liquidate non-landmark properties at half invested cost, take one emergency loan, or declare bankruptcy. Loan principal covers the shortfall, with 20% interest; half of future salaries repays it. Loans reduce net worth.
- Bankruptcy gives remaining cash and properties to a player creditor, preserving buildings. Bank debt returns property to the bank and resets improvements. Bankrupt players are skipped.
- Wins: three built color sets; every purchasable property, including resorts, on a side; every resort; or last survivor. A single color is a toll bonus by default (`GameConfig.ColorSetWins` enables an explicit variant). At `MaxTurns`, the highest nonbankrupt net worth wins; player index breaks ties deterministically.
- Net worth is cash plus purchase/construction book value minus outstanding loan balance.

## Explicit unverified design choices

All default numeric economy values, additional construction sequencing choices, exact partial-building toll stacking, double exceptions, island timing/fines, travel fee/salary behavior, Olympics accumulation/cap, fortune deck/probabilities, emergency-loan terms, liquidation values, landmark owner-liquidation restriction, bankruptcy property transfer, inclusion of resorts in line wins and turn-limit tie-breaking are reconstruction choices awaiting code/runtime corroboration. The guide's forced-sale protection does not by itself prohibit an owner's voluntary sale; the current debt UI conservatively does not offer landmark liquidation.

Prices are intentionally not claimed to be original. APK raw financial arrays are retained but their column semantics and cumulative/incremental structure have not been code-verified. The engine's board constructor accepts a `TileDefinition[32]` override so verified economics can replace the defaults without changing game flow.

## API and deterministic replay

`new Game(seed, humanCount, playerCount, maxTurns, definitions)` or `new Game(GameConfig, definitions)` starts a game. `GetActions()` is the only supported action menu; `Execute(id)` rejects invalid actions without recording them. `ChooseAiAction()` always selects from the legal menu. Unity can animate old/new positions after execution. `ReplayActions` stores successful action IDs; `Game.Replay(...)` reproduces a match from the same configuration and board definitions. RNG is a seeded xorshift32 stream independent of Unity/System.Random and view frame timing.

`CoreSelfTests.Run()` runs 22 logical cases including 64 complete AI matches, state invariants and exact action replay. Internal `TestLand`, `TestMove`, `TestRoll` and `TestCheckVictory` are narrow test setup hooks; ordinary gameplay must not call them. Explicit diagnostic preview flags use test setup for visual verification. Standalone runner is under `Validation/CoreRunner.cs`.

Independent building purchase rules, UI and official guide reference: `Documentation/construction.md`. Online construction hash includes the full building mask and landmark flag.


## Optional body economy mode

EnableBodyEconomy selects the v1.4 negative account and body-sale rules. New local and Steam rooms enable it. Legacy rules above remain for configurations with this flag false. The current specification and implementation boundaries are documented in Documentation/body-economy/README.md. Body sales never directly kill or disable a player. Mandatory payments are uncapped, optional investments use credit, repayment maturity is a final liquidation phase, and monopoly does not end this mode.
