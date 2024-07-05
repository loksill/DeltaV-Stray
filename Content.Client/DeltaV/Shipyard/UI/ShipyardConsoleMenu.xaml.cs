using Content.Client.UserInterface.Controls;
using Content.Shared.Access.Systems;
using Content.Shared.Shipyard;
using Content.Shared.Shipyard.Prototypes;
using Content.Shared.Whitelist;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;

namespace Content.Client.DeltaV.Shipyard.UI;

[GenerateTypedNameReferences]
public sealed partial class ShipyardConsoleMenu : FancyWindow
{
    private readonly AccessReaderSystem _access;
    private readonly IPlayerManager _player;

    public event Action<string>? OnPurchased;

    private readonly List<VesselPrototype> _vessels = new();
    private readonly List<string> _categories = new();

    public Entity<ShipyardConsoleComponent> Console;
    private string? _category;

    public ShipyardConsoleMenu(EntityUid console, IPrototypeManager proto, IEntityManager entMan, IPlayerManager player, AccessReaderSystem access, EntityWhitelistSystem whitelist)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        Console = (console, entMan.GetComponent<ShipyardConsoleComponent>(console));
        _access = access;
        _player = player;

        // don't include ships that aren't allowed by whitelist, server won't accept them anyway
        foreach (var vessel in proto.EnumeratePrototypes<VesselPrototype>())
        {
            if (whitelist.IsWhitelistPassOrNull(vessel.Whitelist, console))
                _vessels.Add(vessel);
        }
        _vessels.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase));

        // only list categories in said ships
        foreach (var vessel in _vessels)
        {
            foreach (var category in vessel.Categories)
            {
                if (!_categories.Contains(category))
                    _categories.Add(category);
            }
        }

        _categories.Sort();
        // inserting here and not adding at the start so it doesn't get affected by sort
        _categories.Insert(0, Loc.GetString("cargo-console-menu-populate-categories-all-text"));
        PopulateCategories();

        SearchBar.OnTextChanged += _ => PopulateProducts();
        Categories.OnItemSelected += args =>
        {
            _category = args.Id == 0 ? null : _categories[args.Id];
            Categories.SelectId(args.Id);
            PopulateProducts();
        };
    }

    /// <summary>
    ///     Populates the list of products that will actually be shown, using the current filters.
    /// </summary>
    private void PopulateProducts()
    {
        Vessels.RemoveAllChildren();

        var access = _player.LocalSession?.AttachedEntity is {} player
            && _access.IsAllowed(player, Console);

        var search = SearchBar.Text.Trim().ToLowerInvariant();
        foreach (var vessel in _vessels)
        {
            if (search.Length != 0 && !vessel.Name.ToLowerInvariant().Contains(search))
                continue;
            if (_category != null && !vessel.Categories.Contains(_category))
                continue;

            var vesselEntry = new VesselRow(vessel, access);
            vesselEntry.OnPurchasePressed += () => OnPurchased?.Invoke(vessel.ID);
            Vessels.AddChild(vesselEntry);
        }
    }

    /// <summary>
    ///     Populates the list categories that will actually be shown, using the current filters.
    /// </summary>
    private void PopulateCategories()
    {
        Categories.Clear();
        foreach (var category in _categories)
        {
            Categories.AddItem(category);
        }
    }

    public void UpdateState(ShipyardConsoleState state)
    {
        BankAccountLabel.Text = Loc.GetString("cargo-console-menu-points-amount", ("amount", state.Balance.ToString()));
        PopulateProducts();
    }
}
