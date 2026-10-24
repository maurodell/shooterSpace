#import <UIKit/UIKit.h>

// Golpe háptico del Taptic Engine para Haptics.Vibrate en iOS. Handheld.Vibrate usa la vibración del sistema (~0.4 s,
// sin control de duración); UIImpactFeedbackGenerator da un golpe corto, comparable a la vibración breve de Android.
// En equipos sin soporte de feedback háptico (iPhone 6s/SE de 1.ª generación, iPad) no hace nada.
extern "C" void NavesHaptics_ImpactHeavy(void)
{
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
    [generator impactOccurred];
#if !__has_feature(objc_arc)
    [generator release];
#endif
}
