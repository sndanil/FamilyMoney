namespace FamilyMoney.Configuration;

public interface IGlobalConfiguration
{
    RootConfiguration Get();

    DatabaseConfiguration GetSelectedDatabase();

    void Save(RootConfiguration configuration);
}
