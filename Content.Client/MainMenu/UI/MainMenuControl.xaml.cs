﻿using Content.Client.Changelog;
using Content.Client.Parallax;
using Robust.Client.AutoGenerated;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;

namespace Content.Client.MainMenu.UI
{
    [GenerateTypedNameReferences]
    public partial class MainMenuControl : Control
        {
            public MainMenuControl(IResourceCache resCache, IConfigurationManager configMan)
            {
                RobustXamlLoader.Load(this);

                LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);

                LayoutContainer.SetAnchorPreset(VBox, LayoutContainer.LayoutPreset.TopRight);
                LayoutContainer.SetMarginRight(VBox, -25);
                LayoutContainer.SetMarginTop(VBox, 30);
                LayoutContainer.SetGrowHorizontal(VBox, LayoutContainer.GrowDirection.Begin);

                var logoTexture = resCache.GetResource<TextureResource>("/Textures/Logo/logo.png");
                Logo.Texture = logoTexture;

                var currentUserName = configMan.GetCVar(CVars.PlayerName);
                UsernameBox.Text = currentUserName;

#if !FULL_RELEASE
                JoinPublicServerButton.Disabled = true;
                JoinPublicServerButton.ToolTip = Loc.GetString("main-menu-join-public-server-button-tooltip");
#endif

                LayoutContainer.SetAnchorPreset(VersionLabel, LayoutContainer.LayoutPreset.BottomRight);
                LayoutContainer.SetGrowHorizontal(VersionLabel, LayoutContainer.GrowDirection.Begin);
                LayoutContainer.SetGrowVertical(VersionLabel, LayoutContainer.GrowDirection.Begin);
            }
        }
}
