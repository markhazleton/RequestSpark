
namespace RequestSpark.Extensions;
/// <summary>
/// Extension Methods for Compare Runner
/// </summary>
public static class CompareRunner_Extensions
{
    private static List<CompareInstance> GetCompareInstances()
    {
        return
            [
                new() {Name="MH", BaseUrl="https://ui.makeboldspark.com/"},
                new() {Name="AZ", BaseUrl="https://samplecrud.azurewebsites.net/"}
            ];
    }
    private static List<CompareRequest> GetCompareRequests()
    {
        return
            [
                new() { Path="status", RequestMethod = HttpVerb.GET, RequiresClientToken=false }
            ];
    }
    private static List<CompareUser> GetCompareUsers()
    {
#pragma warning disable CRRSP06 // A misspelled word has been found
        var list = new List<CompareUser>();
        var user = new CompareUser()
        {
            UserName = "markhazleton",
            Password = RequestSpark.Domain.Constants.DomainConstants.PlaceholderPassword
        };
        user.Properties.Add("username", user.UserName);
        user.Properties.Add("user_firstname", "Mark");
        user.Properties.Add("user_lastname", "Hazleton");
        user.Properties.Add("user_zipcode", "76262");
        user.Properties.Add("user_email", "mark.hazleton@gmail.com");
        user.Properties.Add("today", "2021-10-01");
        user.Properties.Add("today5", "2021-10-05");
        user.Properties.Add("today15", "2021-10-15");
        list.Add(user);
        return list;
#pragma warning restore CRRSP06 // A misspelled word has been found
    }

    /// <summary>
    /// Initialize the Compare Runner
    /// </summary>
    /// <param name="runner"></param>
    public static void InitializeCompareRunner(this CompareRunner runner)
    {
        runner.Requests = GetCompareRequests();
        runner.Instances = GetCompareInstances();
        runner.Users = GetCompareUsers();
    }
}

