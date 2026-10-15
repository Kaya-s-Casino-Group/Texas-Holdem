# iPhone development build

Unity menu: **Tools → Poker → Build Current Scene for iPhone**.
The menu saves the active scene if needed and exports only that scene to
`Builds/iOS-CurrentScene`. It configures ARM64/IL2CPP, iOS 15 minimum,
`com.linxi.texasholdem`, automatic signing with the existing personal team,
and the pixel poker app icon. Orientation is restricted to landscape left/right;
portrait and upside-down portrait are disabled. It does not replace the normal
Build Settings scene list.

Open `Builds/iOS-CurrentScene/Unity-iPhone.xcodeproj`, select `Unity-iPhone`
and the connected iPhone, then build and run. The initial installation may
require trusting the developer in the iPhone's VPN & Device Management settings.

## VAT data contract

`RigidVATAssetImport.ConfigureTexture` explicitly pins Standalone, iPhone and
Android imports to RGBAHalf or RGBAFloat according to the manifest's component
precision. Compression, Crunch and alpha splitting are disabled. Linear color,
Point filtering, Clamp wrapping, original dimensions and no mipmaps are required.
The iPhone build menu reimports and checks both position and rotation textures
before exporting. Do not optimize these data textures using ASTC, ETC, PVRTC,
DXT, color-space conversion or downscaling.

## UIScene compatibility

Unity 2022.3.62f3 exports an older UIApplication lifecycle. iOS 27 rejects apps
built with the iOS 27 SDK unless they adopt UIScene. The observed native crash
is `___UIApplicationEvaluateRuntimeIssueForNoSceneLifecycleAdoption_block_invoke`.

`Assets/Editor/PokerIOSSceneLifecycle.cs` adds the single-window scene manifest
and copies `PokerSceneLifecycle.mm` into the exported UnityFramework target.
The bridge delays Unity window creation until a UIWindowScene is connected,
then forwards active/inactive and background/foreground events to the original
Unity controller. It also forwards URL and user-activity events.

The hook skips exports with Unity's own `Classes/UI/UnityScene.mm` or an existing
scene manifest, so it does not replace a newer engine's implementation. After
an engine upgrade, test startup, pause/resume, orientation and any native plugins
again. This compatibility bridge is project-maintained, not an engine upgrade.

References:
- [Apple UIScene migration](https://developer.apple.com/documentation/uikit/transitioning-to-the-uikit-scene-based-life-cycle)
- [Unity lifecycle support announcement](https://discussions.unity.com/t/info-apple-update-your-editor-to-receive-uiscene-lifecycle-support/1709065)

## Local diagnostics

- `Builds/iOS-CurrentScene/build-status.txt`: Unity export status.
- `Builds/iOS-CurrentScene/build-messages.txt`: Unity export warnings and errors.
- `Builds/ios-xcodebuild.log`: native compilation and signing output.
- `Builds/ios-device-run.log`: captured device startup output.

These generated files are ignored by Git. The project icon source is
`Assets/Art/AppIcon/PokerPixelIcon.png`; the built-in image generation prompt is
saved locally in `ArtSource/AppIcon/pixel-icon-prompt.txt`.

## Verified on 2026-10-16

- Unity 2022.3.62f3 exported `SingleBattleDemoNew` with zero build errors.
- Xcode 27.0 compiled and signed the ARM64 development build successfully.
- iPhone 17 Pro / iOS 27.0.1 installed and launched the application after trusting
  the personal developer certificate. The UIScene startup crash was resolved.
- The game survived a switch to Settings and back with the same process ID;
  landscape rendering was captured in `Builds/iphone-current-scene.png`.
- All four statue VAT position/rotation textures retained their source dimensions
  and RGBAHalf format, with explicit uncompressed iPhone/Android overrides.
- Runtime reported the existing 256x256 additional-light shadow atlas reducing
  shadow resolution to fit 12 maps. This is a rendering-quality warning; VAT data
  was not compressed. Full gameplay and VAT animation playback are not yet tested
  end-to-end on the device.
