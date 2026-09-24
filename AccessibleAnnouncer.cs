using System.Windows.Forms.Automation;

namespace ClearVault;

// Announces a status message to screen readers via UI Automation's
// notification event, which NVDA/JAWS speak immediately without moving
// focus. This replaces the older "focus a read-only TextBox so its new
// text gets read" trick -- that trick worked but yanked keyboard focus
// away from wherever the user actually was (e.g. out of a file list they
// were arrowing through), which is exactly the kind of surprise this app
// exists to avoid.
static class AccessibleAnnouncer
{
    public static void Announce(TextBox statusBox, string message)
    {
        statusBox.Text = message;
        statusBox.AccessibilityObject.RaiseAutomationNotification(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.MostRecent,
            message);
    }
}
