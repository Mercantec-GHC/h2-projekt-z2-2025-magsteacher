using System.DirectoryServices.Protocols;
using System.Text;
using System.Net;

namespace API.Services
{
    /// <summary>
    /// Service til håndtering af Active Directory autentificering og brugerinformation
    /// </summary>
    public class ActiveDirectoryService
    {
        private readonly ILogger<ActiveDirectoryService> _logger;
        private readonly IConfiguration _configuration;
        
        // AD konfiguration fra appsettings.json
        private readonly string _server;
        private readonly string _username;
        private readonly string _password;
        private readonly string _domain;
        private readonly int _port;
        private readonly bool _useSSL;
        private readonly int _connectionTimeout;
        private readonly int _maxRetries;
        private readonly int _retryDelayMs;

        /// <summary>
        /// Initialiserer en ny instans af ActiveDirectoryService
        /// </summary>
        /// <param name="logger">Logger til fejlrapportering</param>
        /// <param name="configuration">Konfiguration til AD indstillinger</param>
        public ActiveDirectoryService(ILogger<ActiveDirectoryService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            // Læs AD konfiguration fra appsettings.json
            _server = _configuration["ActiveDirectory:Server"] ?? "10.133.71.100";
            _domain = _configuration["ActiveDirectory:Domain"] ?? "mags.local";
            _username = _configuration["ActiveDirectory:ReaderUsername"] ?? "adReader";
            _password = _configuration["ActiveDirectory:ReaderPassword"] ?? "Merc1234!";
            _port = int.Parse(_configuration["ActiveDirectory:Port"] ?? "389");
            _useSSL = bool.Parse(_configuration["ActiveDirectory:UseSSL"] ?? "false");
            _connectionTimeout = int.Parse(_configuration["ActiveDirectory:ConnectionTimeout"] ?? "30");
            _maxRetries = int.Parse(_configuration["ActiveDirectory:MaxRetries"] ?? "3");
            _retryDelayMs = int.Parse(_configuration["ActiveDirectory:RetryDelayMs"] ?? "1000");
        }

        /// <summary>
        /// Autentificerer en bruger mod Active Directory
        /// </summary>
        /// <param name="username">Brugernavn (kan være email eller sAMAccountName)</param>
        /// <param name="password">Adgangskode</param>
        /// <returns>ADUserInfo med brugerinformation hvis autentificering lykkes, ellers null</returns>
        public async Task<ADUserInfo?> AuthenticateUserAsync(string username, string password)
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Forsøger AD autentificering for bruger: {Username} (forsøg {Attempt}/{MaxRetries})", 
                        username, attempt, _maxRetries);

                    // Test netværksforbindelse først
                    if (!await TestNetworkConnectivityAsync())
                    {
                        _logger.LogError("Netværksforbindelse til AD server {Server}:{Port} fejler", _server, _port);
                        if (attempt < _maxRetries)
                        {
                            await Task.Delay(_retryDelayMs * attempt);
                            continue;
                        }
                        return null;
                    }

                    // Opret LDAP forbindelse med timeout
                    using var connection = new LdapConnection(new LdapDirectoryIdentifier(_server, _port));
                    
                    // Konfigurer forbindelse med timeout
                    connection.SessionOptions.ProtocolVersion = 3;
                    connection.SessionOptions.SecureSocketLayer = _useSSL;
                    connection.SessionOptions.VerifyServerCertificate = (conn, cert) => true;
                    connection.Timeout = TimeSpan.FromSeconds(_connectionTimeout);

                    // Opret credentials for AD reader bruger
                    var networkCredential = new NetworkCredential(_username, _password, _domain);
                    connection.Credential = networkCredential;

                    // Åbn forbindelse med retry
                    await Task.Run(() => connection.Bind());

                    _logger.LogInformation("AD forbindelse etableret succesfuldt");

                    // Søg efter brugeren i AD
                    var userInfo = await SearchUserInADAsync(connection, username);
                    
                    if (userInfo == null)
                    {
                        _logger.LogWarning("Bruger {Username} ikke fundet i AD", username);
                        return null;
                    }

                    // Test brugerens credentials
                    var userCredentials = new NetworkCredential(userInfo.SamAccountName, password, _domain);
                    using var userConnection = new LdapConnection(new LdapDirectoryIdentifier(_server, _port));
                    userConnection.SessionOptions.ProtocolVersion = 3;
                    userConnection.SessionOptions.SecureSocketLayer = _useSSL;
                    userConnection.SessionOptions.VerifyServerCertificate = (conn, cert) => true;
                    userConnection.Timeout = TimeSpan.FromSeconds(_connectionTimeout);

                    userConnection.Credential = userCredentials;

                    // Test autentificering
                    await Task.Run(() => userConnection.Bind());

                    _logger.LogInformation("AD autentificering succesfuldt for bruger: {Username}", username);

                    return userInfo;
                }
                catch (LdapException ex)
                {
                    _logger.LogError(ex, "LDAP fejl ved autentificering af bruger: {Username}. Error: {ErrorCode} (forsøg {Attempt}/{MaxRetries})", 
                        username, ex.ErrorCode, attempt, _maxRetries);
                    
                    if (attempt < _maxRetries)
                    {
                        await Task.Delay(_retryDelayMs * attempt);
                        continue;
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Generel fejl ved AD autentificering for bruger: {Username} (forsøg {Attempt}/{MaxRetries})", 
                        username, attempt, _maxRetries);
                    
                    if (attempt < _maxRetries)
                    {
                        await Task.Delay(_retryDelayMs * attempt);
                        continue;
                    }
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Søger efter en bruger i Active Directory
        /// </summary>
        /// <param name="connection">LDAP forbindelse</param>
        /// <param name="username">Brugernavn eller email at søge efter</param>
        /// <returns>ADUserInfo hvis brugeren findes, ellers null</returns>
        private async Task<ADUserInfo?> SearchUserInADAsync(LdapConnection connection, string username)
        {
            try
            {
                // Konstruer søgning - søg både på sAMAccountName og email
                var searchFilter = $"(|(sAMAccountName={username})(mail={username})(userPrincipalName={username}))";
                var searchRequest = new SearchRequest(
                    $"DC={_domain.Split('.')[0]},DC={_domain.Split('.')[1]}", // Konstruer base DN
                    searchFilter,
                    SearchScope.Subtree,
                    "sAMAccountName", "mail", "displayName", "givenName", "sn", "memberOf", "userPrincipalName"
                );

                var searchResponse = await Task.Run(() => (SearchResponse)connection.SendRequest(searchRequest));

                if (searchResponse.Entries.Count == 0)
                {
                    _logger.LogWarning("Ingen bruger fundet i AD for: {Username}", username);
                    return null;
                }

                var entry = searchResponse.Entries[0];
                
                // Debug: Log alle tilgængelige attributter
                _logger.LogInformation("Tilgængelige attributter for bruger:");
                foreach (string attrName in entry.Attributes.AttributeNames)
                {
                    _logger.LogInformation("Attribut: {AttrName}, Værdier: {Count}", attrName, entry.Attributes[attrName].Count);
                }
                
                var userInfo = new ADUserInfo
                {
                    SamAccountName = GetAttributeValue(entry, "sAMAccountName"),
                    Email = GetAttributeValue(entry, "mail"),
                    DisplayName = GetAttributeValue(entry, "displayName"),
                    FirstName = GetAttributeValue(entry, "givenName"),
                    LastName = GetAttributeValue(entry, "sn"),
                    UserPrincipalName = GetAttributeValue(entry, "userPrincipalName"),
                    Groups = GetGroupMemberships(entry)
                };

                _logger.LogInformation("Bruger fundet i AD: {SamAccountName}, Email: {Email}, Groups: {GroupCount}", 
                    userInfo.SamAccountName, userInfo.Email, userInfo.Groups.Count);

                return userInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved søgning i AD for bruger: {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// Henter værdien af et attribut fra en AD entry
        /// </summary>
        private string GetAttributeValue(SearchResultEntry entry, string attributeName)
        {
            if (entry.Attributes[attributeName] != null && entry.Attributes[attributeName].Count > 0)
            {
                return entry.Attributes[attributeName][0].ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// Henter gruppemedlemskaber fra en AD entry
        /// </summary>
        private List<string> GetGroupMemberships(SearchResultEntry entry)
        {
            var groups = new List<string>();
            
            _logger.LogInformation("Henter gruppemedlemskaber for bruger");
            
            if (entry.Attributes["memberOf"] != null)
            {
                _logger.LogInformation("memberOf attribut fundet med {Count} grupper", entry.Attributes["memberOf"].Count);
                
                foreach (var group in entry.Attributes["memberOf"])
                {
                    var groupDn = group.ToString();
                    _logger.LogInformation("Gruppe DN: {GroupDn}", groupDn);
                    
                    if (!string.IsNullOrEmpty(groupDn))
                    {
                        // Ekstraher gruppenavn fra DN (f.eks. "CN=Admins,OU=Groups,DC=mags,DC=local" -> "Admins")
                        var cnIndex = groupDn.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
                        if (cnIndex >= 0)
                        {
                            var cnEnd = groupDn.IndexOf(",", cnIndex);
                            if (cnEnd > cnIndex)
                            {
                                var groupName = groupDn.Substring(cnIndex + 3, cnEnd - cnIndex - 3);
                                groups.Add(groupName);
                                _logger.LogInformation("Tilføjet gruppe: {GroupName}", groupName);
                            }
                            else
                            {
                                // Hvis der ikke er et komma efter CN, så er det hele gruppenavnet
                                var groupName = groupDn.Substring(cnIndex + 3);
                                groups.Add(groupName);
                                _logger.LogInformation("Tilføjet gruppe (uden komma): {GroupName}", groupName);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Kunne ikke finde CN= i gruppe DN: {GroupDn}", groupDn);
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("memberOf attribut ikke fundet for bruger");
            }

            _logger.LogInformation("Samlet grupper fundet: {Groups}", string.Join(", ", groups));
            return groups;
        }

        /// <summary>
        /// Tester netværksforbindelse til AD server
        /// </summary>
        /// <returns>True hvis forbindelsen er tilgængelig, ellers false</returns>
        private async Task<bool> TestNetworkConnectivityAsync()
        {
            try
            {
                _logger.LogInformation("Tester netværksforbindelse til AD server {Server}:{Port}", _server, _port);
                
                using var client = new System.Net.NetworkInformation.Ping();
                var reply = await client.SendPingAsync(_server, 5000); // 5 sekunder timeout
                
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    _logger.LogInformation("Ping til AD server {Server} succesfuldt (tid: {RoundtripTime}ms)", 
                        _server, reply.RoundtripTime);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Ping til AD server {Server} fejlede med status: {Status}", 
                        _server, reply.Status);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved ping test til AD server {Server}:{Port}", _server, _port);
                return false;
            }
        }

        /// <summary>
        /// Tester LDAP forbindelse til AD server
        /// </summary>
        /// <returns>True hvis LDAP forbindelsen virker, ellers false</returns>
        public async Task<bool> TestLDAPConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Tester LDAP forbindelse til AD server {Server}:{Port}", _server, _port);

                using var connection = new LdapConnection(new LdapDirectoryIdentifier(_server, _port));
                connection.SessionOptions.ProtocolVersion = 3;
                connection.SessionOptions.SecureSocketLayer = _useSSL;
                connection.SessionOptions.VerifyServerCertificate = (conn, cert) => true;
                connection.Timeout = TimeSpan.FromSeconds(_connectionTimeout);

                var networkCredential = new NetworkCredential(_username, _password, _domain);
                connection.Credential = networkCredential;

                await Task.Run(() => connection.Bind());

                _logger.LogInformation("LDAP forbindelse til AD server {Server}:{Port} succesfuldt", _server, _port);
                return true;
            }
            catch (LdapException ex)
            {
                _logger.LogError(ex, "LDAP forbindelse fejlede til AD server {Server}:{Port}. Error: {ErrorCode}", 
                    _server, _port, ex.ErrorCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generel fejl ved LDAP forbindelse til AD server {Server}:{Port}", _server, _port);
                return false;
            }
        }

        /// <summary>
        /// Mapper AD grupper til applikationsroller
        /// </summary>
        /// <param name="adGroups">Liste af AD grupper</param>
        /// <returns>Applikationsrolle navn</returns>
        public string MapADGroupToRole(List<string> adGroups)
        {
            // Mapping logik - tilpas efter jeres AD gruppestruktur
            if (adGroups.Any(g => g.Contains("Admin", StringComparison.OrdinalIgnoreCase) || 
                                 g.Contains("Administrator", StringComparison.OrdinalIgnoreCase)))
            {
                return "Admin";
            }
            
            if (adGroups.Any(g => g.Contains("Manager", StringComparison.OrdinalIgnoreCase)))
            {
                return "Manager";
            }
            
            if (adGroups.Any(g => g.Contains("Receptionist", StringComparison.OrdinalIgnoreCase) ||
                                 g.Contains("Reception", StringComparison.OrdinalIgnoreCase)))
            {
                return "Receptionist";
            }
            
            if (adGroups.Any(g => g.Contains("User", StringComparison.OrdinalIgnoreCase)))
            {
                return "User";
            }

            // Default rolle for AD brugere er Receptionist
            return "Receptionist";
        }
    }

    /// <summary>
    /// Model til AD brugerinformation
    /// </summary>
    public class ADUserInfo
    {
        /// <summary>
        /// SAM Account Name fra Active Directory
        /// </summary>
        public string SamAccountName { get; set; } = string.Empty;
        
        /// <summary>
        /// Email adresse fra Active Directory
        /// </summary>
        public string Email { get; set; } = string.Empty;
        
        /// <summary>
        /// Display Name fra Active Directory
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Fornavn fra Active Directory
        /// </summary>
        public string FirstName { get; set; } = string.Empty;
        
        /// <summary>
        /// Efternavn fra Active Directory
        /// </summary>
        public string LastName { get; set; } = string.Empty;
        
        /// <summary>
        /// User Principal Name fra Active Directory
        /// </summary>
        public string UserPrincipalName { get; set; } = string.Empty;
        
        /// <summary>
        /// Liste af grupper brugeren er medlem af i Active Directory
        /// </summary>
        public List<string> Groups { get; set; } = new List<string>();
    }
}
