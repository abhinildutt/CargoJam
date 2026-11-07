// Thin bridge to UNUserNotificationCenter for CargoJam.Game.Meta.LocalNotifications.
// Requests are identified as "cargojam.<id>", so scheduling the same id again replaces it.

#import <Foundation/Foundation.h>
#import <UserNotifications/UserNotifications.h>

static NSString *StringFrom(const char *text)
{
    return text != NULL ? [NSString stringWithUTF8String:text] : @"";
}

extern "C"
{
    void CargoJam_NotifyRequestAuthorization()
    {
        UNAuthorizationOptions options = UNAuthorizationOptionAlert | UNAuthorizationOptionSound;
        [[UNUserNotificationCenter currentNotificationCenter]
            requestAuthorizationWithOptions:options
                          completionHandler:^(BOOL granted, NSError *error) {}];
    }

    void CargoJam_NotifySchedule(int identifier, const char *title, const char *body, double delaySeconds)
    {
        if (delaySeconds < 1) delaySeconds = 1;
        UNMutableNotificationContent *content = [[UNMutableNotificationContent alloc] init];
        content.title = StringFrom(title);
        content.body = StringFrom(body);
        content.sound = [UNNotificationSound defaultSound];

        UNTimeIntervalNotificationTrigger *trigger =
            [UNTimeIntervalNotificationTrigger triggerWithTimeInterval:delaySeconds repeats:NO];
        NSString *requestId = [NSString stringWithFormat:@"cargojam.%d", identifier];
        UNNotificationRequest *request =
            [UNNotificationRequest requestWithIdentifier:requestId content:content trigger:trigger];
        [[UNUserNotificationCenter currentNotificationCenter] addNotificationRequest:request withCompletionHandler:nil];
    }

    void CargoJam_NotifyCancelAll()
    {
        UNUserNotificationCenter *center = [UNUserNotificationCenter currentNotificationCenter];
        [center removeAllPendingNotificationRequests];
        [center removeAllDeliveredNotifications];
    }
}
