// Thin bridge to UIKit's feedback generators for CargoJam.Game.Feel.Haptics.
// Generators are kept alive and re-prepared after each use so the next tap fires with minimal latency.

#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *impactGenerators[5];
static UINotificationFeedbackGenerator *notificationGenerator;
static UISelectionFeedbackGenerator *selectionGenerator;

static UIImpactFeedbackStyle StyleFor(int style)
{
    switch (style)
    {
        case 1: return UIImpactFeedbackStyleMedium;
        case 2: return UIImpactFeedbackStyleHeavy;
        case 3: return UIImpactFeedbackStyleSoft;
        case 4: return UIImpactFeedbackStyleRigid;
        default: return UIImpactFeedbackStyleLight;
    }
}

extern "C"
{
    void CargoJam_HapticsPrepare()
    {
        for (int i = 0; i < 5; i++)
        {
            if (impactGenerators[i] == nil)
                impactGenerators[i] = [[UIImpactFeedbackGenerator alloc] initWithStyle:StyleFor(i)];
            [impactGenerators[i] prepare];
        }
        if (notificationGenerator == nil) notificationGenerator = [[UINotificationFeedbackGenerator alloc] init];
        if (selectionGenerator == nil) selectionGenerator = [[UISelectionFeedbackGenerator alloc] init];
        [notificationGenerator prepare];
        [selectionGenerator prepare];
    }

    // style: 0 light, 1 medium, 2 heavy, 3 soft, 4 rigid
    void CargoJam_HapticsImpact(int style, float intensity)
    {
        if (style < 0 || style > 4) style = 0;
        if (impactGenerators[style] == nil) CargoJam_HapticsPrepare();
        [impactGenerators[style] impactOccurredWithIntensity:intensity];
        [impactGenerators[style] prepare];
    }

    // type: 0 success, 1 warning, 2 error (matches UINotificationFeedbackType)
    void CargoJam_HapticsNotify(int type)
    {
        if (notificationGenerator == nil) CargoJam_HapticsPrepare();
        [notificationGenerator notificationOccurred:(UINotificationFeedbackType)type];
        [notificationGenerator prepare];
    }

    void CargoJam_HapticsSelection()
    {
        if (selectionGenerator == nil) CargoJam_HapticsPrepare();
        [selectionGenerator selectionChanged];
        [selectionGenerator prepare];
    }
}
