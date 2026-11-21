// index.html's boot guard: main.js could be read and has started.
globalThis.athloBootStarted = true;

// Notices a new deploy and reloads (or offers to) — see update.js.
import('./update.js').then(m => m.start()).catch(() => { });
// Phone keyboards: finish the word being typed before another field takes the focus — see ime-fix.js.
import('./ime-fix.js').catch(() => { });

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// dotnet.js is loaded here, not with a static import, so a failed download is caught below and
// the loading screen can say so (the boot guard) instead of animating forever.
try {
    const { dotnet } = await import('./_framework/dotnet.js');
    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        // The ICU shard is chosen from the application culture (default: browser language,
        // usually giving EFIGS, which has no Greek). Pin el-GR so the Greek shard loads.
        .withApplicationCulture('el-GR')
        .create();

    const config = dotnetRuntime.getConfig();

    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (err) {
    // Only shown while the loading screen is still up (index.html).
    if (globalThis.athloBootFailed) globalThis.athloBootFailed(err);
}
