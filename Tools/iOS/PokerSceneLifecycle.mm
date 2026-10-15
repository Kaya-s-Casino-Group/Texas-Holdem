#import "UnityAppController.h"
#import <UIKit/UIKit.h>

// UIScene bridge for Unity 2022.3.62f3's single-window application controller.
// The export hook omits this file when Unity provides its own scene lifecycle.
@interface PokerAppController : UnityAppController
@property(nonatomic, strong) NSDictionary* pokerLaunchOptions;
@property(nonatomic, weak) UIWindowScene* pokerStartupScene;
- (void)pokerConnectScene:(UIWindowScene*)scene;
@end

@interface PokerSceneDelegate : UIResponder<UIWindowSceneDelegate>
@property(nonatomic, strong) UIWindow* window;
@end

@implementation PokerAppController
- (BOOL)application:(UIApplication*)application didFinishLaunchingWithOptions:(NSDictionary*)options
{
    // UIKit has not connected the window scene yet. Initialize Unity from
    // willConnectToSession so its window is created with a valid UIWindowScene.
    self.pokerLaunchOptions = options;
    return YES;
}

- (UIWindowScene*)pickStartupWindowScene:(NSSet<UIScene*>*)scenes
{
    return self.pokerStartupScene;
}

- (void)pokerConnectScene:(UIWindowScene*)scene
{
    self.pokerStartupScene = scene;
    [super application:UIApplication.sharedApplication didFinishLaunchingWithOptions:self.pokerLaunchOptions];
    self.pokerLaunchOptions = nil;
    self.window.windowScene = scene;
}
@end

@implementation PokerSceneDelegate
- (void)scene:(UIScene*)scene willConnectToSession:(UISceneSession*)session options:(UISceneConnectionOptions*)options
{
    if (![scene isKindOfClass:UIWindowScene.class]) return;
    PokerAppController* app = (PokerAppController*)GetAppController();
    [app pokerConnectScene:(UIWindowScene*)scene];
    self.window = app.window;
    for (UIOpenURLContext* context in options.URLContexts)
        [app application:UIApplication.sharedApplication openURL:context.URL options:@{}];
    for (NSUserActivity* activity in options.userActivities)
        [app application:UIApplication.sharedApplication continueUserActivity:activity restorationHandler:^(NSArray* objects) {}];
}

- (void)sceneDidBecomeActive:(UIScene*)scene
{
    [GetAppController() applicationDidBecomeActive:UIApplication.sharedApplication];
}

- (void)sceneWillResignActive:(UIScene*)scene
{
    [GetAppController() applicationWillResignActive:UIApplication.sharedApplication];
}

- (void)sceneWillEnterForeground:(UIScene*)scene
{
    [GetAppController() applicationWillEnterForeground:UIApplication.sharedApplication];
}

- (void)sceneDidEnterBackground:(UIScene*)scene
{
    [GetAppController() applicationDidEnterBackground:UIApplication.sharedApplication];
}

- (void)scene:(UIScene*)scene openURLContexts:(NSSet<UIOpenURLContext*>*)contexts
{
    for (UIOpenURLContext* context in contexts)
    {
        NSMutableDictionary* options = [NSMutableDictionary dictionary];
        if (context.options.sourceApplication)
            options[UIApplicationOpenURLOptionsSourceApplicationKey] = context.options.sourceApplication;
        if (context.options.annotation)
            options[UIApplicationOpenURLOptionsAnnotationKey] = context.options.annotation;
        options[UIApplicationOpenURLOptionsOpenInPlaceKey] = @(context.options.openInPlace);
        [GetAppController() application:UIApplication.sharedApplication openURL:context.URL options:options];
    }
}

- (void)scene:(UIScene*)scene continueUserActivity:(NSUserActivity*)activity
{
    [GetAppController() application:UIApplication.sharedApplication continueUserActivity:activity restorationHandler:^(NSArray* objects) {}];
}
@end

IMPL_APP_CONTROLLER_SUBCLASS(PokerAppController)
