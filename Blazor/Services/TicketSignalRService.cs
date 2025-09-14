using Microsoft.AspNetCore.SignalR.Client;
using System.ComponentModel;

namespace Blazor.Services
{
    /// <summary>
    /// Service for SignalR kommunikation med ticket systemet
    /// Håndterer real-time chat og notifikationer
    /// </summary>
    public class TicketSignalRService : INotifyPropertyChanged, IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private readonly ILogger<TicketSignalRService> _logger;
        private readonly string _hubUrl;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Func<string, string, string, bool, DateTime, Task>? MessageReceived;
        public event Func<string, string, DateTime, Task>? UserJoined;
        public event Func<string, string, DateTime, Task>? UserLeft;
        public event Func<string, bool, DateTime, Task>? TypingIndicator;
        public event Func<string, string, string, DateTime, Task>? StatusUpdated;
        public event Func<string, string, string, DateTime, Task>? TicketAssigned;
        public event Func<string, string, string, DateTime, Task>? TicketClosed;
        public event Func<string, string, string, string, DateTime, Task>? CommentAdded;
        public event Func<string, string, string, string, DateTime, Task>? TicketCreated;
        public event Func<string, string, string, DateTime, Task>? TicketUpdated;
        public event Func<string, Task>? Error;
        public event Func<string, Task>? Connected;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
        public string? CurrentTicketId { get; private set; }
        public string? CurrentUserId { get; private set; }
        public string? CurrentUsername { get; private set; }
        public string? CurrentUserRole { get; private set; }

        public TicketSignalRService(ILogger<TicketSignalRService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _hubUrl = configuration["ApiBaseUrl"] + "/tickethub";
        }

        /// <summary>
        /// Initialiser SignalR forbindelse
        /// </summary>
        public async Task InitializeAsync(string token)
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .WithAutomaticReconnect()
                .Build();

            // Registrer event handlers
            RegisterEventHandlers();

            try
            {
                await _hubConnection.StartAsync();
                _logger.LogInformation("SignalR forbindelse etableret");
                OnPropertyChanged(nameof(IsConnected));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved etablering af SignalR forbindelse");
                Error?.Invoke("Kunne ikke forbinde til chat systemet");
            }
        }

        /// <summary>
        /// Tilslut til en ticket chat
        /// </summary>
        public async Task JoinTicketAsync(string ticketId, string userId, string username, string userRole)
        {
            if (_hubConnection?.State != HubConnectionState.Connected)
            {
                Error?.Invoke("Ikke forbundet til chat systemet");
                return;
            }

            try
            {
                CurrentTicketId = ticketId;
                CurrentUserId = userId;
                CurrentUsername = username;
                CurrentUserRole = userRole;

                await _hubConnection.InvokeAsync("JoinTicketGroup", ticketId);
                _logger.LogInformation("Tilsluttet til ticket {TicketId}", ticketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved tilslutning til ticket {TicketId}", ticketId);
                Error?.Invoke("Kunne ikke tilslutte til ticket chat");
            }
        }

        /// <summary>
        /// Forlad ticket chat
        /// </summary>
        public async Task LeaveTicketAsync()
        {
            if (_hubConnection?.State != HubConnectionState.Connected || string.IsNullOrEmpty(CurrentTicketId))
            {
                return;
            }

            try
            {
                await _hubConnection.InvokeAsync("LeaveTicketGroup", CurrentTicketId);
                _logger.LogInformation("Forladt ticket {TicketId}", CurrentTicketId);
                
                CurrentTicketId = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved forladelse af ticket {TicketId}", CurrentTicketId);
            }
        }

        /// <summary>
        /// Send besked til ticket chat
        /// </summary>
        public async Task SendMessageAsync(string message, bool isInternal = false)
        {
            if (_hubConnection?.State != HubConnectionState.Connected || string.IsNullOrEmpty(CurrentTicketId))
            {
                Error?.Invoke("Ikke forbundet til chat systemet");
                return;
            }

            try
            {
                await _hubConnection.InvokeAsync("SendMessage", CurrentTicketId, message, isInternal);
                _logger.LogInformation("Besked sendt til ticket {TicketId}", CurrentTicketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved afsendelse af besked til ticket {TicketId}", CurrentTicketId);
                Error?.Invoke("Kunne ikke sende besked");
            }
        }

        /// <summary>
        /// Send typing indikator
        /// </summary>
        public async Task SendTypingIndicatorAsync(bool isTyping)
        {
            if (_hubConnection?.State != HubConnectionState.Connected || string.IsNullOrEmpty(CurrentTicketId))
            {
                return;
            }

            try
            {
                await _hubConnection.InvokeAsync("SendTypingIndicator", CurrentTicketId, isTyping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved afsendelse af typing indikator");
            }
        }

        /// <summary>
        /// Send status opdatering
        /// </summary>
        public async Task SendStatusUpdateAsync(string status, string message)
        {
            if (_hubConnection?.State != HubConnectionState.Connected || string.IsNullOrEmpty(CurrentTicketId))
            {
                Error?.Invoke("Ikke forbundet til chat systemet");
                return;
            }

            try
            {
                await _hubConnection.InvokeAsync("SendStatusUpdate", CurrentTicketId, status, message);
                _logger.LogInformation("Status opdatering sendt for ticket {TicketId}", CurrentTicketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved afsendelse af status opdatering");
                Error?.Invoke("Kunne ikke opdatere status");
            }
        }

        /// <summary>
        /// Send ticket tildeling notifikation
        /// </summary>
        public async Task SendAssignmentNotificationAsync(string assigneeId, string assigneeName)
        {
            if (_hubConnection?.State != HubConnectionState.Connected || string.IsNullOrEmpty(CurrentTicketId))
            {
                Error?.Invoke("Ikke forbundet til chat systemet");
                return;
            }

            try
            {
                await _hubConnection.InvokeAsync("SendAssignmentNotification", CurrentTicketId, assigneeId, assigneeName);
                _logger.LogInformation("Tildeling notifikation sendt for ticket {TicketId}", CurrentTicketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved afsendelse af tildeling notifikation");
                Error?.Invoke("Kunne ikke sende tildeling notifikation");
            }
        }

        /// <summary>
        /// Send ticket lukning notifikation
        /// </summary>
        public async Task SendTicketClosedNotificationAsync(string resolution, string closedBy)
        {
            if (_hubConnection?.State != HubConnectionState.Connected || string.IsNullOrEmpty(CurrentTicketId))
            {
                Error?.Invoke("Ikke forbundet til chat systemet");
                return;
            }

            try
            {
                await _hubConnection.InvokeAsync("SendTicketClosedNotification", CurrentTicketId, resolution, closedBy);
                _logger.LogInformation("Ticket lukning notifikation sendt for ticket {TicketId}", CurrentTicketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fejl ved afsendelse af ticket lukning notifikation");
                Error?.Invoke("Kunne ikke sende lukning notifikation");
            }
        }

        /// <summary>
        /// Registrer event handlers for SignalR events
        /// </summary>
        private void RegisterEventHandlers()
        {
            if (_hubConnection == null) return;

            _hubConnection.On<string, string, string, bool, DateTime>("MessageReceived", async (id, ticketId, message, isInternal, timestamp) =>
            {
                if (MessageReceived != null)
                    await MessageReceived(id, ticketId, message, isInternal, timestamp);
            });

            _hubConnection.On<string, string, DateTime>("UserJoined", async (username, userId, timestamp) =>
            {
                if (UserJoined != null)
                    await UserJoined(username, userId, timestamp);
            });

            _hubConnection.On<string, string, DateTime>("UserLeft", async (username, userId, timestamp) =>
            {
                if (UserLeft != null)
                    await UserLeft(username, userId, timestamp);
            });

            _hubConnection.On<string, bool, DateTime>("TypingIndicator", async (username, isTyping, timestamp) =>
            {
                if (TypingIndicator != null)
                    await TypingIndicator(username, isTyping, timestamp);
            });

            _hubConnection.On<string, string, string, DateTime>("StatusUpdated", async (ticketId, status, message, timestamp) =>
            {
                if (StatusUpdated != null)
                    await StatusUpdated(ticketId, status, message, timestamp);
            });

            _hubConnection.On<string, string, string, DateTime>("TicketAssigned", async (ticketId, assigneeId, assigneeName, timestamp) =>
            {
                if (TicketAssigned != null)
                    await TicketAssigned(ticketId, assigneeId, assigneeName, timestamp);
            });

            _hubConnection.On<string, string, string, DateTime>("TicketClosed", async (ticketId, resolution, closedBy, timestamp) =>
            {
                if (TicketClosed != null)
                    await TicketClosed(ticketId, resolution, closedBy, timestamp);
            });

            _hubConnection.On<string, string, string, string, DateTime>("CommentAdded", async (commentId, ticketId, message, authorName, timestamp) =>
            {
                if (CommentAdded != null)
                    await CommentAdded(commentId, ticketId, message, authorName, timestamp);
            });

            _hubConnection.On<string, string, string, string, DateTime>("TicketCreated", async (ticketId, ticketNumber, title, serviceType, timestamp) =>
            {
                if (TicketCreated != null)
                    await TicketCreated(ticketId, ticketNumber, title, serviceType, timestamp);
            });

            _hubConnection.On<string, string, string, DateTime>("TicketUpdated", async (ticketId, status, priority, timestamp) =>
            {
                if (TicketUpdated != null)
                    await TicketUpdated(ticketId, status, priority, timestamp);
            });

            _hubConnection.On<string>("Error", async (message) =>
            {
                if (Error != null)
                    await Error(message);
            });

            _hubConnection.On<string>("Connected", async (message) =>
            {
                if (Connected != null)
                    await Connected(message);
            });

            _hubConnection.On<string>("JoinedTicket", (message) =>
            {
                // Handle joined ticket confirmation
            });

            // Connection state changed events
            _hubConnection.Closed += async (error) =>
            {
                _logger.LogWarning("SignalR forbindelse lukket: {Error}", error?.Message);
                OnPropertyChanged(nameof(IsConnected));
                
                if (error != null)
                {
                    Error?.Invoke("Forbindelse til chat systemet mistet");
                }
            };

            _hubConnection.Reconnected += async (connectionId) =>
            {
                _logger.LogInformation("SignalR forbindelse genoprettet: {ConnectionId}", connectionId);
                OnPropertyChanged(nameof(IsConnected));
            };

            _hubConnection.Reconnecting += async (error) =>
            {
                _logger.LogWarning("SignalR forbindelse genoprettes: {Error}", error?.Message);
                OnPropertyChanged(nameof(IsConnected));
            };
        }

        /// <summary>
        /// Trigger PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Dispose SignalR forbindelse
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
        }
    }
}
