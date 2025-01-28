using Content.Client._RMC14.LinkAccount;
using Content.Client._RMC14.Roadmap;
using Content.Client.Changelog;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;

namespace Content.Client.Info
{
    public sealed class LinkBanner : BoxContainer
    {
        private readonly IConfigurationManager _cfg;

        private ValueList<(CVarDef<string> cVar, Button button)> _infoLinks;

        public LinkBanner()
        {
            var buttons = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal
            };
            AddChild(buttons);

            var uriOpener = IoCManager.Resolve<IUriOpener>();
            _cfg = IoCManager.Resolve<IConfigurationManager>();

            var rulesButton = new Button() {Text = Loc.GetString("server-info-rules-button")};
            rulesButton.OnPressed += args => new RulesAndInfoWindow().Open();
            buttons.AddChild(rulesButton);

            AddInfoButton("server-info-discord-button", CCVars.InfoLinksDiscord);
            AddInfoButton("server-info-website-button", CCVars.InfoLinksWebsite);
            AddInfoButton("server-info-wiki-button", CCVars.InfoLinksWiki);
            AddInfoButton("server-info-forum-button", CCVars.InfoLinksForum);
            AddInfoButton("server-info-telegram-button", CCVars.InfoLinksTelegram);

            var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
            var guidebookButton = new Button() { Text = Loc.GetString("server-info-guidebook-button") };
            guidebookButton.OnPressed += _ =>
            {
                guidebookController.ToggleGuidebook();
            };
            buttons.AddChild(guidebookButton);

            var changelogButton = new ChangelogButton();
            changelogButton.OnPressed += args => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow();
            buttons.AddChild(changelogButton);

            var roadmapButton = new Button
            {
                Text = Loc.GetString("rmc-roadmap-window-title"),
                StyleClasses = { StyleBase.ButtonCaution }
            };
            roadmapButton.OnPressed += _ => UserInterfaceManager.GetUIController<RoadmapUIController>().ToggleRoadmap();
            buttons.AddChild(roadmapButton);

            var linkAccount = UserInterfaceManager.GetUIController<LinkAccountUIController>();
            var linkAccountButton = new Button
            {
                Text = Loc.GetString("rmc-ui-link-discord-account"),
            };
            linkAccountButton.OnPressed += _ => linkAccount.ToggleWindow();
            buttons.AddChild(linkAccountButton);

            AddInfoButton("rmc-ui-patreon", CCVars.InfoLinksPatreon);

            void AddInfoButton(string loc, CVarDef<string> cVar)
            {
                var button = new Button { Text = Loc.GetString(loc) };
                button.OnPressed += _ => uriOpener.OpenUri(_cfg.GetCVar(cVar));
                buttons.AddChild(button);
                _infoLinks.Add((cVar, button));
            }
        }

        protected override void EnteredTree()
        {
            // LinkBanner is constructed before the client even connects to the server due to UI refactor stuff.
            // We need to update these buttons when the UI is shown.

            base.EnteredTree();

            foreach (var (cVar, link) in _infoLinks)
            {
                link.Visible = _cfg.GetCVar(cVar) != "";
            }
        }
    }
}
