// Họ tên: Hà văn Mạnh
// MSSV: 202418941




using System.Text;

Console.OutputEncoding = Encoding.UTF8;

// ==========================================
// 1. KICH BAN KIEM THU (TOP-LEVEL STATEMENTS)
// ==========================================

var alarm = new Alarm();
var doorController = new DoorController();
var ventilationController = new VentilationController();
var mediator = new EmergencyMediator(alarm, doorController, ventilationController);

var legacyDetector = new LegacySmokeDetector(320); // 32.0% -> Binh thuong
ISensor smokeSensor = new SmokeSensorAdapter(legacyDetector);

var monitor = new SensorMonitor();
var dashboard = new DashboardObserver();
var emailAlert = new EmailAlertObserver();
var auditLog = new AuditLogObserver();
var emergencyBridge = new EmergencyBridgeObserver(mediator);

monitor.Attach(dashboard);
monitor.Attach(dashboard); // Khong cho phep dang ky trung
monitor.Attach(emailAlert);
monitor.Attach(auditLog);
monitor.Attach(emergencyBridge);

Console.WriteLine("=== KICH BAN 1: Muc khoi binh thuong (32.0%) ===");
monitor.CheckSensor("ServerRoom-RackA", smokeSensor);
Console.WriteLine("(Duoi 50% -> Khong phat canh bao)\n");

Console.WriteLine("=== KICH BAN 2: Muc khoi canh bao WARNING (58.0%) ===");
legacyDetector.SetSmokeLevel(580);
monitor.CheckSensor("ServerRoom-RackA", smokeSensor);
Console.WriteLine();

Console.WriteLine("=== KICH BAN 3: Muc khoi nguy hiem CRITICAL (73.5%) ===");
legacyDetector.SetSmokeLevel(735);
monitor.CheckSensor("ServerRoom-RackA", smokeSensor);
Console.WriteLine();

Console.WriteLine("=== KICH BAN 4: Huy dang ky EmailAlertObserver & kiem tra lai ===");
monitor.Detach(emailAlert);
legacyDetector.SetSmokeLevel(820);
monitor.CheckSensor("ServerRoom-RackA", smokeSensor);
Console.WriteLine();

Console.WriteLine("=== KICH BAN 5: Mot observer loi khong lam anh huong observer khac ===");
monitor.Attach(new FaultyObserver());
legacyDetector.SetSmokeLevel(750);
monitor.CheckSensor("ServerRoom-RackA", smokeSensor);

// ==========================================
// 2. MO HINH KHUNG & ADAPTER PATTERN
// ==========================================

public interface ISensor
{
    double ReadValue();
    string GetUnit();
}

public sealed record AlertEvent(
    string SensorName,
    double Value,
    string Unit,
    string Severity,
    DateTime OccurredAt);

public sealed class LegacySmokeDetector
{
    private int smokeLevel;

    public LegacySmokeDetector(int smokeLevel)
    {
        this.smokeLevel = smokeLevel;
    }

    public int GetSmokeLevel() => smokeLevel; // 0..1000

    public string GetDescription() => "Legacy smoke detector";

    public void SetSmokeLevel(int value)
    {
        smokeLevel = Math.Clamp(value, 0, 1000);
    }
}

public sealed class SmokeSensorAdapter : ISensor
{
    private readonly LegacySmokeDetector legacy;

    public SmokeSensorAdapter(LegacySmokeDetector legacy)
    {
        this.legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
    }

    public double ReadValue()
    {
        return legacy.GetSmokeLevel() / 10.0;
    }

    public string GetUnit() => "% smoke";
}

// ==========================================
// 3. OBSERVER PATTERN
// ==========================================

public interface IAlertObserver
{
    void Update(AlertEvent alertEvent);
}

public sealed class SensorMonitor
{
    private readonly List<IAlertObserver> observers = new();

    public void Attach(IAlertObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        if (!observers.Contains(observer))
        {
            observers.Add(observer);
        }
    }

    public void Detach(IAlertObserver observer)
    {
        observers.Remove(observer);
    }

    public void CheckSensor(string sensorName, ISensor sensor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sensorName);
        ArgumentNullException.ThrowIfNull(sensor);

        double value = sensor.ReadValue();
        string? severity = value switch
        {
            >= 70 => "CRITICAL",
            >= 50 => "WARNING",
            _ => null
        };

        if (severity is null)
        {
            return;
        }

        var alertEvent = new AlertEvent(
            sensorName,
            value,
            sensor.GetUnit(),
            severity,
            DateTime.Now);

        Notify(alertEvent);
    }

    private void Notify(AlertEvent alertEvent)
    {
        foreach (IAlertObserver observer in observers.ToArray())
        {
            try
            {
                observer.Update(alertEvent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MONITOR ERROR] {observer.GetType().Name} gap loi: {ex.Message}");
            }
        }
    }
}

public sealed class DashboardObserver : IAlertObserver
{
    public void Update(AlertEvent alertEvent)
    {
        Console.WriteLine(
            $"[DASHBOARD] {alertEvent.SensorName}: " +
            $"{alertEvent.Value:F1}{alertEvent.Unit} " +
            $"[{alertEvent.Severity}]");
    }
}

public sealed class EmailAlertObserver : IAlertObserver
{
    public void Update(AlertEvent alertEvent)
    {
        Console.WriteLine(
            $"[EMAIL] Gui canh bao {alertEvent.Severity} luc {alertEvent.OccurredAt:HH:mm:ss} " +
            $"- {alertEvent.SensorName}: {alertEvent.Value:F1}{alertEvent.Unit}");
    }
}

public sealed class AuditLogObserver : IAlertObserver
{
    public void Update(AlertEvent alertEvent)
    {
        Console.WriteLine(
            $"[AUDIT LOG] {alertEvent.OccurredAt:O} | {alertEvent.SensorName} | " +
            $"{alertEvent.Value:F1}{alertEvent.Unit} | {alertEvent.Severity}");
    }
}

public sealed class FaultyObserver : IAlertObserver
{
    public void Update(AlertEvent alertEvent)
    {
        throw new InvalidOperationException("Mat ket noi toi dich vu phu");
    }
}

public sealed class EmergencyBridgeObserver : IAlertObserver
{
    private readonly IEmergencyMediator mediator;

    public EmergencyBridgeObserver(IEmergencyMediator mediator)
    {
        this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public void Update(AlertEvent alertEvent)
    {
        if (alertEvent.Severity == "CRITICAL")
        {
            Console.WriteLine($"[BRIDGE] Phat hien CRITICAL tu {alertEvent.SensorName} -> Gui FIRE_DETECTED toi Mediator");
            mediator.Notify(this, "FIRE_DETECTED");
        }
    }
}

// ==========================================
// 4. MEDIATOR PATTERN
// ==========================================

public interface IEmergencyMediator
{
    void Notify(object sender, string eventCode);
}

public sealed class Alarm
{
    private IEmergencyMediator? mediator;

    public void SetMediator(IEmergencyMediator mediator) => this.mediator = mediator;

    public void Activate() => Console.WriteLine("[ALARM] Coi bao dong khan cap DA BAT.");

    public void Reset() => Console.WriteLine("[ALARM] Coi bao dong da tat.");
}

public sealed class DoorController
{
    private IEmergencyMediator? mediator;

    public void SetMediator(IEmergencyMediator mediator) => this.mediator = mediator;

    public void UnlockAllExitDoors() => Console.WriteLine("[DOOR] Da mo khoa toan bo cua thoat hiem phong may.");

    public void LockDoors() => Console.WriteLine("[DOOR] Da khoa lai cua an ninh.");
}

public sealed class VentilationController
{
    private IEmergencyMediator? mediator;

    public void SetMediator(IEmergencyMediator mediator) => this.mediator = mediator;

    public void ShutDownFreshAirAndExhaustSmoke()
        => Console.WriteLine("[VENTILATION] Da ngat cap khi tuoi va bat quat hut khoi khan cap.");

    public void RestoreNormalAirflow()
        => Console.WriteLine("[VENTILATION] Da khoi phuc thong gio binh thuong.");
}

public sealed class EmergencyMediator : IEmergencyMediator
{
    private readonly Alarm alarm;
    private readonly DoorController doorController;
    private readonly VentilationController ventilationController;

    public EmergencyMediator(
        Alarm alarm,
        DoorController doorController,
        VentilationController ventilationController)
    {
        this.alarm = alarm ?? throw new ArgumentNullException(nameof(alarm));
        this.doorController = doorController ?? throw new ArgumentNullException(nameof(doorController));
        this.ventilationController = ventilationController ?? throw new ArgumentNullException(nameof(ventilationController));

        this.alarm.SetMediator(this);
        this.doorController.SetMediator(this);
        this.ventilationController.SetMediator(this);
    }

    public void Notify(object sender, string eventCode)
    {
        if (eventCode == "FIRE_DETECTED")
        {
            alarm.Activate();
            doorController.UnlockAllExitDoors();
            ventilationController.ShutDownFreshAirAndExhaustSmoke();
        }
        else if (eventCode == "ALL_CLEAR")
        {
            alarm.Reset();
            doorController.LockDoors();
            ventilationController.RestoreNormalAirflow();
        }
    }
}