// Native iOS haptics bridge for Arrows.Core.Haptics (Taptic Engine via UIFeedbackGenerator).
#import <UIKit/UIKit.h>

extern "C" void _ArrowsHaptic(int type)
{
    switch (type)
    {
        case 0: [[[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight] impactOccurred]; break;
        case 1: [[[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium] impactOccurred]; break;
        case 2: [[[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy] impactOccurred]; break;
        case 3: [[[UINotificationFeedbackGenerator alloc] init] notificationOccurred:UINotificationFeedbackTypeSuccess]; break;
        case 4: [[[UINotificationFeedbackGenerator alloc] init] notificationOccurred:UINotificationFeedbackTypeError]; break;
        default: break;
    }
}
