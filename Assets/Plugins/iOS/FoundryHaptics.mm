// Foundry's haptic bridge (STORY-3.4). Own code, no third-party — two thin wrappers over
// UIKit's feedback generators, called from Game.App/Haptics.cs via DllImport("__Internal").
#import <UIKit/UIKit.h>

extern "C" {

void _foundryHapticImpact(int strength) {
    UIImpactFeedbackStyle style = strength <= 0 ? UIImpactFeedbackStyleLight
                                : strength == 1 ? UIImpactFeedbackStyleMedium
                                                : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
    [generator prepare];
    [generator impactOccurred];
}

void _foundryHapticNotify(int type) {
    UINotificationFeedbackType feedback = type == 0 ? UINotificationFeedbackTypeSuccess
                                        : type == 1 ? UINotificationFeedbackTypeWarning
                                                    : UINotificationFeedbackTypeError;
    UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
    [generator prepare];
    [generator notificationOccurred:feedback];
}

}
