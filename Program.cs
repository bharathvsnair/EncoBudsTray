using EncoBudsTray;

ApplicationConfiguration.Initialize();
using var app = new TrayApplication();
Application.Run(app);
