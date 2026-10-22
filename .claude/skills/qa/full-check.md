# General and full QA additions

Use these additions for `general`, `full` and `release`; they are not required for every incremental change check.

**General check.** Add these to the picked features every time:

- **A real session, start to end,** with 2 pads and immunity, waves running and enemies unfrozen. Launch, then the lobby: the Surge platform, the labels and team pick if Teams is on. Play a Surge run for 3 waves: pick an ability, cast it and its ultimate, and take a perk choice. Then the lobby and its titles screen, the F1 and F2 windows, one versus round (team versus if Teams is on), the pause menu, Mods > SpiderSurge with every page opened, and General > Reload the settings file. Wait on `error` through all of it.
- **Startup:** the log from launch to the lobby has no new error or warning, every part says it loaded, and the config has every key with sane values.
- **Performance:** garbage per frame and fps in the lobby, in a Surge wave with F1 and F2 open, and in a versus round (recipes.md, "Performance"). Compare with the numbers in history.md; if they are higher, measure the release DLL in the same states.
- **One combined check per hot spot** of a picked feature, with every patch on it active at once.
