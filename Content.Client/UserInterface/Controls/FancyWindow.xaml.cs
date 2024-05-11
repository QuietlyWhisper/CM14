﻿using System.Numerics;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;

namespace Content.Client.UserInterface.Controls
{
    [GenerateTypedNameReferences]
    [Virtual]
    public partial class FancyWindow : BaseWindow
    {
        [Dependency] private readonly IEntitySystemManager _sysMan = default!;
        private GuidebookSystem? _guidebookSystem;
        private const int DRAG_MARGIN_SIZE = 7;
        public const string StyleClassWindowHelpButton = "windowHelpButton";

        public FancyWindow()
        {
            RobustXamlLoader.Load(this);

            CloseButton.OnPressed += _ => Close();
            HelpButton.OnPressed += _ => Help();
            XamlChildren = ContentsContainer.Children;
        }

        public string? Title
        {
            get => WindowTitle.Text;
            set => WindowTitle.Text = value;
        }

        private List<string>? _helpGuidebookIds;
        public List<string>? HelpGuidebookIds
        {
            get => _helpGuidebookIds;
            set
            {
                _helpGuidebookIds = value;
                HelpButton.Disabled = _helpGuidebookIds == null;
                HelpButton.Visible = !HelpButton.Disabled;
            }
        }

        public void Help()
        {
            if (HelpGuidebookIds is null)
                return;
            _guidebookSystem ??= _sysMan.GetEntitySystem<GuidebookSystem>();
            _guidebookSystem.OpenHelp(HelpGuidebookIds);
        }

        protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            var mode = DragMode.Move;

            if (Resizable)
            {
                if (relativeMousePos.Y < DRAG_MARGIN_SIZE)
                {
                    mode = DragMode.Top;
                }
                else if (relativeMousePos.Y > Size.Y - DRAG_MARGIN_SIZE)
                {
                    mode = DragMode.Bottom;
                }

                if (relativeMousePos.X < DRAG_MARGIN_SIZE)
                {
                    mode |= DragMode.Left;
                }
                else if (relativeMousePos.X > Size.X - DRAG_MARGIN_SIZE)
                {
                    mode |= DragMode.Right;
                }
            }

            return mode;
        }
    }
}
