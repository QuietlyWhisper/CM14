using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Ghost;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._RMC14.UserInterface.Systems.Ghost.Controls
{
    [GenerateTypedNameReferences]
    public sealed partial class RMCGhostTargetWindow : DefaultWindow
    {
        private string _searchText = string.Empty;

        /// <summary>
        /// Nested dictionary, first string corresponds to a department, second string is a special state or location, the tuple in the end is the warp data.
        /// </summary>
        private Dictionary<string, Dictionary<string, List<(string, NetEntity, string)>>> _categories = new();

        public event Action<NetEntity>? WarpClicked;
        public event Action? OnGhostnadoClicked;

        public RMCGhostTargetWindow()
        {
            RobustXamlLoader.Load(this);
            SearchBar.OnTextChanged += OnSearchTextChanged;

            GhostnadoButton.OnPressed += _ => OnGhostnadoClicked?.Invoke();
        }

        /// <summary>
        /// Categorizes the warps into a nested structure where the first key is the area and the second key is the department.
        /// </summary>
        /// <param name="warps">A collection of warp data.</param>
        public void UpdateWarps(IEnumerable<RMCGhostWarp> warps)
        {
            _categories.Clear();

            foreach (var warp in warps)
            {
                if (!_categories.ContainsKey(warp.Area))
                {
                    _categories[warp.Area] = new Dictionary<string, List<(string, NetEntity, string)>>();
                }

                if (!_categories[warp.Area].ContainsKey(warp.CategoryName))
                {
                    _categories[warp.Area][warp.CategoryName] = new List<(string, NetEntity, string)>();
                }

                var warpName = warp.IsWarpPoint
                    ? Loc.GetString("ghost-target-window-current-button", ("name", warp.DisplayName))
                    : warp.DisplayName;

                _categories[warp.Area][warp.CategoryName].Add((warpName, warp.Entity, warp.WarpColor));
            }
        }


        public void Populate()
        {
            ButtonContainer.DisposeAllChildren();
            PopulateContainer();
        }

        /// <summary>
        ///  Populate the container with buttons and labels corresponding to the area, department, and warp.
        /// </summary>
        private void PopulateContainer()
        {
            foreach (var (areaName, departments) in _categories)
            {
                var hasWarps = departments.Values.Any(warps => warps.Count > 0);
                if (!hasWarps)
                    continue;

                var areaLabel = new Label
                {
                    Text = areaName,
                    HorizontalAlignment = HAlignment.Left,
                    VerticalAlignment = VAlignment.Center,
                };

                ButtonContainer.AddChild(areaLabel);

                foreach (var (departmentName, warps) in departments)
                {
                    if (warps.Count == 0)
                        continue;

                    var departmentLabel = new Label
                    {
                        Text = departmentName,
                        HorizontalAlignment = HAlignment.Center,
                        VerticalAlignment = VAlignment.Center,
                        SizeFlagsStretchRatio = 1,
                    };

                    ButtonContainer.AddChild(departmentLabel);

                    foreach (var (name, warpTarget, colorHex) in warps)
                    {
                        var currentButtonRef = new Button
                        {
                            Text = name,
                            TextAlign = Label.AlignMode.Right,
                            HorizontalAlignment = HAlignment.Center,
                            VerticalAlignment = VAlignment.Center,
                            SizeFlagsStretchRatio = 1,
                            Modulate = Color.FromHex(colorHex),
                            MinSize = new Vector2(400, 20),
                            ClipText = true,
                        };

                        currentButtonRef.OnPressed += _ => WarpClicked?.Invoke(warpTarget);
                        currentButtonRef.Visible = ButtonIsVisible(currentButtonRef);

                        ButtonContainer.AddChild(currentButtonRef);
                    }
                }
            }
        }

        private bool ButtonIsVisible(Button button)
        {
            return string.IsNullOrEmpty(_searchText) || button.Text == null || button.Text.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateVisibleButtons()
        {
            foreach (var child in ButtonContainer.Children)
            {
                if (child is Button button)
                    button.Visible = ButtonIsVisible(button);
            }
        }

        private void OnSearchTextChanged(LineEdit.LineEditEventArgs args)
        {
            _searchText = args.Text;

            UpdateVisibleButtons();
            GhostScroll.SetScrollValue(Vector2.Zero);
        }
    }
}
