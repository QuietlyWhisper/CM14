﻿using Content.Client._RMC14.UserInterface;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.SupplyDrop;

[GenerateTypedNameReferences]
public sealed partial class SupplyDropWindow : DefaultWindow
{
    public readonly FloatSpinBox Longitude;
    public readonly FloatSpinBox Latitude;
    public TimeSpan LastUpdateAt;
    public TimeSpan NextUpdateAt;

    public SupplyDropWindow()
    {
        RobustXamlLoader.Load(this);

        Longitude = UIExtensions.CreateDialSpinBox(buttons: false);
        TargetXContainer.AddChild(Longitude);

        Latitude = UIExtensions.CreateDialSpinBox(buttons: false);
        TargetYContainer.AddChild(Latitude);

        Longitude.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.GuiTabNavigateNext ||
                args.Function == EngineKeyFunctions.GuiTabNavigatePrev)
            {
                Latitude.GrabKeyboardFocus();
            }
        };

        Latitude.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.GuiTabNavigateNext ||
                args.Function == EngineKeyFunctions.GuiTabNavigatePrev)
            {
                Longitude.GrabKeyboardFocus();
            }
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var time = IoCManager.Resolve<IGameTiming>().CurTime;
        var cooldown = NextUpdateAt - time;
        if (cooldown < TimeSpan.Zero)
        {
            LaunchButton.Disabled = false;
            CooldownBar.Visible = false;
            LaunchStatusLabel.Visible = true;
            return;
        }

        LaunchButton.Disabled = true;
        CooldownBar.Visible = true;
        LaunchStatusLabel.Visible = false;
        CooldownBar.MinValue = (float) LastUpdateAt.TotalSeconds;
        CooldownBar.MaxValue = (float) NextUpdateAt.TotalSeconds;
        CooldownBar.Value = (float) (LastUpdateAt.TotalSeconds + NextUpdateAt.TotalSeconds - time.TotalSeconds);
        CooldownLabel.Text = $"{(int) cooldown.TotalSeconds} seconds until next launch";
    }
}
