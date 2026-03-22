#import <Foundation/Foundation.h>
#import <CoreMotion/CoreMotion.h>

/// Unity IL2CPP / Mono: C#에서 등록한 콜백으로 '오늘 0시~현재' 걸음 수를 전달합니다.

static CMPedometer *s_pedometer = nil;
static void (*s_unityCallback)(int) = NULL;

extern "C" {

void NativePedometer_SetCallback(void (*callback)(int))
{
    s_unityCallback = callback;
}

void NativePedometer_QueryTodaySteps(void)
{
    if (s_unityCallback == NULL)
        return;

    if (![CMPedometer isStepCountingAvailable])
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (s_unityCallback != NULL)
                s_unityCallback(0);
        });
        return;
    }

    if (s_pedometer == nil)
        s_pedometer = [[CMPedometer alloc] init];

    NSCalendar *cal = [NSCalendar currentCalendar];
    NSDate *start = [cal startOfDayForDate:[NSDate date]];
    NSDate *end = [NSDate date];

    [s_pedometer queryPedometerDataFromDate:start
                                     toDate:end
                                    toQueue:[NSOperationQueue mainQueue]
                                withHandler:^(CMPedometerData *_Nullable data, NSError *_Nullable error) {
        int steps = 0;
        if (data != nil && error == nil)
            steps = (int)[data.numberOfSteps integerValue];

        if (s_unityCallback != NULL)
            s_unityCallback(steps);
    }];
}

void NativePedometer_Release(void)
{
    s_unityCallback = NULL;
    s_pedometer = nil;
}

}
