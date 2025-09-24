SET NOCOUNT ON;

IF OBJECT_ID('dbo.InvoiceLines', 'U') IS NOT NULL DROP TABLE dbo.InvoiceLines;
IF OBJECT_ID('dbo.Invoices', 'U') IS NOT NULL DROP TABLE dbo.Invoices;
IF OBJECT_ID('dbo.Appointments', 'U') IS NOT NULL DROP TABLE dbo.Appointments;
IF OBJECT_ID('dbo.Vehicles', 'U') IS NOT NULL DROP TABLE dbo.Vehicles;
IF OBJECT_ID('dbo.Customers', 'U') IS NOT NULL DROP TABLE dbo.Customers;

CREATE TABLE dbo.Customers (
    CustomerId UNIQUEIDENTIFIER PRIMARY KEY,
    DisplayName NVARCHAR(256) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    Phone NVARCHAR(32) NOT NULL,
    Line1 NVARCHAR(256) NOT NULL,
    Line2 NVARCHAR(256) NULL,
    City NVARCHAR(128) NOT NULL,
    State NVARCHAR(64) NOT NULL,
    PostalCode NVARCHAR(32) NOT NULL,
    Country NVARCHAR(64) NOT NULL,
    ModifiedOn DATETIME2 NOT NULL
);

CREATE TABLE dbo.Vehicles (
    VehicleId UNIQUEIDENTIFIER PRIMARY KEY,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    Vin NVARCHAR(32) NOT NULL,
    Make NVARCHAR(64) NOT NULL,
    Model NVARCHAR(64) NOT NULL,
    ModelYear INT NOT NULL,
    Odometer INT NOT NULL,
    CONSTRAINT FK_Vehicles_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId)
);

CREATE TABLE dbo.Invoices (
    InvoiceId UNIQUEIDENTIFIER PRIMARY KEY,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    VehicleId UNIQUEIDENTIFIER NOT NULL,
    InvoiceNumber NVARCHAR(64) NOT NULL,
    InvoiceDate DATETIME2 NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    Status NVARCHAR(32) NOT NULL,
    CONSTRAINT FK_Invoices_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
    CONSTRAINT FK_Invoices_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId)
);

CREATE TABLE dbo.InvoiceLines (
    InvoiceLineId INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId UNIQUEIDENTIFIER NOT NULL,
    Description NVARCHAR(256) NOT NULL,
    Quantity DECIMAL(18,2) NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    TaxAmount DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_InvoiceLines_Invoices FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoices(InvoiceId)
);

CREATE TABLE dbo.Appointments (
    AppointmentId UNIQUEIDENTIFIER PRIMARY KEY,
    CustomerId UNIQUEIDENTIFIER NOT NULL,
    VehicleId UNIQUEIDENTIFIER NOT NULL,
    StartTime DATETIME2 NOT NULL,
    EndTime DATETIME2 NOT NULL,
    Advisor NVARCHAR(128) NOT NULL,
    Status NVARCHAR(32) NOT NULL,
    Location NVARCHAR(128) NOT NULL,
    CONSTRAINT FK_Appointments_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
    CONSTRAINT FK_Appointments_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId)
);

DECLARE @CustomerId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @VehicleId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @InvoiceId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333';
DECLARE @AppointmentId UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444444';

INSERT INTO dbo.Customers (CustomerId, DisplayName, Email, Phone, Line1, Line2, City, State, PostalCode, Country, ModifiedOn)
VALUES
(@CustomerId, 'Ada Lovelace', 'ada@example.com', '+15555550123', '123 Innovation Way', NULL, 'London', 'LDN', 'EC1A 1BB', 'UK', SYSUTCDATETIME());

INSERT INTO dbo.Vehicles (VehicleId, CustomerId, Vin, Make, Model, ModelYear, Odometer)
VALUES
(@VehicleId, @CustomerId, 'VIN00001', 'Tesla', 'Model S', 2022, 12000);

INSERT INTO dbo.Invoices (InvoiceId, CustomerId, VehicleId, InvoiceNumber, InvoiceDate, TotalAmount, Status)
VALUES
(@InvoiceId, @CustomerId, @VehicleId, 'INV-0001', DATEADD(DAY, -3, SYSUTCDATETIME()), 499.95, 'Paid');

INSERT INTO dbo.InvoiceLines (InvoiceId, Description, Quantity, UnitPrice, TaxAmount)
VALUES
(@InvoiceId, 'Diagnostic', 1, 99.95, 9.95),
(@InvoiceId, 'Battery Replacement', 1, 350.00, 35.00);

INSERT INTO dbo.Appointments (AppointmentId, CustomerId, VehicleId, StartTime, EndTime, Advisor, Status, Location)
VALUES
(@AppointmentId, @CustomerId, @VehicleId, DATEADD(DAY, 1, CAST(SYSUTCDATETIME() AS DATE)), DATEADD(HOUR, 2, DATEADD(DAY, 1, CAST(SYSUTCDATETIME() AS DATE))), 'Sam Advisor', 'Scheduled', 'Main Service Bay');
