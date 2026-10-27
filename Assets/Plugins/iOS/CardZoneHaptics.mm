#import <UIKit/UIKit.h>
#import <dispatch/dispatch.h>

extern "C" void CardZoneLightImpact()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if ([UIApplication sharedApplication].applicationState != UIApplicationStateActive) return;
        if (@available(iOS 10.0, *)) {
            static UIImpactFeedbackGenerator *generator;
            if (!generator) generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            [generator impactOccurred];
            [generator prepare];
        }
    });
}
