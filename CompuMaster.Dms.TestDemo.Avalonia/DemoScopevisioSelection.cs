using System.Globalization;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;

namespace CompuMaster.Dms.TestDemo.Avalonia;

// Mirrors ScopevisioDemoSelection in the WinForms demo, including the distinction
// between the OAuth organisation selector and the post-selection tenant guard.
internal static class DemoScopevisioSelection
{
    internal static long? Validate(string organisation, string tenant)
    {
        long? id = null;
        if (!string.IsNullOrEmpty(organisation))
        {
            if (!long.TryParse(organisation.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
                throw new ArgumentException(DemoText.Get("InvalidOrganisationId"));
            id = value;
        }
        if (!string.IsNullOrEmpty(tenant) && (string.IsNullOrWhiteSpace(tenant) || tenant.Any(char.IsControl)))
            throw new ArgumentException("The expected Teamwork tenant must be a nonblank identifier without control characters.");
        return id;
    }
    internal static IDmsLoginProfile CreateProfile(string username, string password, string customer, string organisation, string tenant)
    {
        var id = Validate(organisation, tenant);
        // A tenant guard alone must follow the interactive picker, not select the default organisation.
        if (!id.HasValue) return new DmsLoginProfile { DmsProvider = BaseDmsProvider.DmsProviders.Scopevisio,
            Username = username, Password = password, CustomerInstance = customer };
        var credentials = new ScopevisioLoginCredentials { Username = username, Password = password,
            ClientNumber = customer, CustomerInstance = customer, OrganisationId = id, ExpectedTeamworkTenantId = tenant };
        credentials.Validate();
        return credentials;
    }
    internal static bool AllowsInstanceSwitch(IDmsLoginProfile profile, string tenant) =>
        profile is not ScopevisioLoginCredentials && string.IsNullOrEmpty(tenant);

    internal static async Task<bool> SelectAsync(BaseDmsProvider provider, IDmsLoginProfile profile, string tenant,
        Func<IReadOnlyList<DmsInstanceInfo>, Task<string?>> choose)
    {
        if (profile is not ScopevisioLoginCredentials { OrganisationId: not null } && provider is IDmsInstanceProvider instances)
        {
            var available = await Task.Run(instances.ListAvailableDmsInstances);
            if (available.Count == 0) throw new InvalidOperationException("No DMS instances are available.");
            var id = available.Count == 1 ? available[0].ID : await choose(available);
            if (id is null) return false;
            if (!available.Any(item => item.ID == id)) throw new ArgumentException("The selected DMS instance is not available.");
            if (instances.CurrentDmsInstance?.ID != id) await Task.Run(() => instances.SelectDmsInstance(id));
        }
        if (!string.IsNullOrEmpty(tenant) && (provider as IDmsInstanceProvider)?.CurrentDmsInstance?.ID != tenant)
            throw new InvalidOperationException(DemoText.Get("SelectedTeamworkTenantMismatch"));
        return true;
    }
}
