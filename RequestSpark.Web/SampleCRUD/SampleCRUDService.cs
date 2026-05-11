namespace RequestSpark.Web.SampleCRUD;

public class SampleCRUDService
{
    private readonly HttpClient _client;
    private readonly SampleCRUDClient _crudClient;
    private const string ApiVersion = "1";

    public SampleCRUDService(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient();
        _client.BaseAddress = new Uri("https://ui.makeboldspark.com/");
        _client.Timeout = TimeSpan.FromSeconds(15);
        _crudClient = new SampleCRUDClient(_client);
    }

    // Keep parameterless constructor for backward compatibility
    public SampleCRUDService()
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri("https://ui.makeboldspark.com/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        _crudClient = new SampleCRUDClient(_client);
    }

    public async Task<string> GetEmployeeCount()
    {
        var employees = await _crudClient.EmployeeAllAsync(1, 15, ApiVersion);
        return $"Got {employees.Count} Employees";
    }

    public Task<ICollection<EmployeeDto>> GetAllEmployees(int? pageNumber = 1, int? pageSize = 15)
        => _crudClient.EmployeeAllAsync(pageNumber, pageSize, ApiVersion);

    public async Task<EmployeeDto> GetEmployeeById(int id)
    {
        var response = await _client.GetAsync($"api/employee/{id}?api-version={ApiVersion}");
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();

        var wrappedResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<SampleCrudResourceResponse<EmployeeDto>>(responseText);
        if (wrappedResponse?.Resource != null)
        {
            return wrappedResponse.Resource;
        }

        var employee = Newtonsoft.Json.JsonConvert.DeserializeObject<EmployeeDto>(responseText);
        if (employee != null)
        {
            return employee;
        }

        throw new InvalidOperationException($"Could not parse employee response for id {id}.");
    }

    public Task<EmployeeDto> CreateEmployee(EmployeeDto employee)
        => _crudClient.EmployeePOSTAsync(ApiVersion, employee);

    public Task<EmployeeDto> UpdateEmployee(int id, EmployeeDto employee)
        => _crudClient.EmployeePUTAsync(id, ApiVersion, employee);

    public Task<EmployeeDto> DeleteEmployee(int id)
        => _crudClient.DeleteEmployeeAsync(id, ApiVersion);

    public Task<ICollection<DepartmentDto>> GetDepartments(bool? includeEmployees = null)
        => _crudClient.DepartmentAsync(includeEmployees, ApiVersion);

    public Task<ICollection<DepartmentDto>> GetDepartmentById(int id)
        => _crudClient.Department2Async(id, ApiVersion);

    public Task<ICollection<ApiExplorerModel>> GetApiExplorer()
        => _crudClient.ExplorerAsync(ApiVersion);

    public Task<ApplicationStatus> GetStatus()
        => _crudClient.StatusAsync(ApiVersion);

    private sealed class SampleCrudResourceResponse<T>
    {
        [Newtonsoft.Json.JsonProperty("resource")]
        public T? Resource { get; set; }
    }
}

