
using System;
using System.Collections.Generic;
using UnityEngine;

public class SiloPlantSimulator : MonoBehaviour
{
    public enum Node
    {
        Receiving = 0,
        Holding = 1,
        Silo1 = 2,
        Silo2 = 3,
        Silo3 = 4,
        Silo4 = 5,
        Silo5 = 6,
        Silo6 = 7,
        Silo7 = 8,
        Dryer = 9,
        Dispatch = 10
    }

    public enum Fault
    {
        None,
        ConveyorBlockage,
        BeltSlip,
        LevelSensor,
        ElevatorBearing,
        DryerOverheat,
        GateMismatch
    }

    [Serializable]
    public class Bin
    {
        public string name;
        public float capacity;
        public float tonnes;
        public float moisture = 13f;
        public float temperature = 20f;
        public float impurities = 0.5f;

        public Bin(string name, float capacity, float tonnes)
        {
            this.name = name;
            this.capacity = capacity;
            this.tonnes = tonnes;
        }

        public float Fill
        {
            get { return capacity <= 0 ? 0 : tonnes / capacity; }
        }

        public float Free
        {
            get { return Mathf.Max(0, capacity - tonnes); }
        }
    }

    [Serializable]
    public class Machine
    {
        public string name;
        public bool enabled = true;
        public bool faulted;
        public float temperature = 30f;
        public float speed = 85f;

        public Machine(string name)
        {
            this.name = name;
        }
    }

    [Header("Simulation")]
    [Min(0.1f)] public float simulationSpeed = 1f;
    public bool startPlantOnLaunch = true;
    public bool autoBuildWorld = true;

    [Header("Transfer")]
    [Range(10f, 120f)] public float transferRate = 60f;
    public Node source = Node.Receiving;
    public Node destination = Node.Holding;

    [Header("Dryer and furnace (illustrative game values)")]
    [Range(35f, 90f)] public float dryerSetpoint = 65f;
    [Range(80f, 240f)] public float furnaceSetpoint = 180f;
    [Range(0.1f, 20f)] public float dryerCapacity = 10f;
    [Range(0.1f, 2f)] public float moistureRemovalPerHour = 1f;

    [Header("Training")]
    public int missionIndex;
    public int xp;
    public int credits = 250;

    private readonly List<Bin> bins = new List<Bin>();
    private readonly List<Machine> machines = new List<Machine>();
    private readonly List<string> actions = new List<string>();
    private readonly List<string> alarms = new List<string>();

    private readonly string[] nodeNames =
    {
        "Receiving Port", "Holding Bin",
        "Silo 1", "Silo 2", "Silo 3", "Silo 4",
        "Silo 5", "Silo 6", "Silo 7",
        "Dryer", "Dispatch Bin"
    };

    private readonly string[] missionNames =
    {
        "Safe Receiving",
        "Route and Silo Selection",
        "Fault Diagnosis",
        "Protect Grain Quality",
        "Dispatch and Reconcile Stock"
    };

    private readonly string[] missionDescriptions =
    {
        "Receive grain, check capacity and move it into the holding bin.",
        "Select a suitable storage silo and complete a valid transfer.",
        "Inject a training fault, review its diagnosis and clear it.",
        "Run the quality screen and review the incoming moisture.",
        "Transfer grain to dispatch and record an outgoing truck load."
    };

    private bool initialized;
    private bool transferRunning;
    private bool plantRunning;
    private bool emergencyStop;
    private bool safetyInterlock;
    private bool gateAligned = true;
    private bool qualityScreenCompleted;

    private Fault activeFault = Fault.None;

    private float elapsed;
    private float missionElapsed;
    private float openingStock;
    private float receipts;
    private float dispatches;
    private float losses;
    private float totalMoved;
    private float throughput;
    private float moistureInput = 18f;
    private float qualityScore = 94f;

    private float dryerTemperature = 45f;
    private float furnaceTemperature = 120f;

    private string lastAction = "Waiting for initialization.";
    private string aiMessage = "Welcome, Operator. Check route, capacity and equipment status.";

    public IReadOnlyList<Bin> Bins { get { return bins; } }
    public IReadOnlyList<Machine> Machines { get { return machines; } }
    public IReadOnlyList<string> Actions { get { return actions; } }
    public IReadOnlyList<string> Alarms { get { return alarms; } }
    public IReadOnlyList<string> NodeNames { get { return nodeNames; } }

    public bool IsInitialized { get { return initialized; } }
    public bool TransferRunning { get { return transferRunning; } }
    public bool PlantRunning { get { return plantRunning; } }
    public bool EmergencyStop { get { return emergencyStop; } }
    public bool SafetyInterlock { get { return safetyInterlock; } }
    public bool GateAligned { get { return gateAligned; } }
    public Fault ActiveFault { get { return activeFault; } }

    public float TotalStock { get; private set; }
    public float TotalMoved { get { return totalMoved; } }
    public float Receipts { get { return receipts; } }
    public float Dispatches { get { return dispatches; } }
    public float Losses { get { return losses; } }
    public float OpeningStock { get { return openingStock; } }
    public float Throughput { get { return throughput; } }
    public float QualityScore { get { return qualityScore; } }
    public float MoistureInput { get { return moistureInput; } }
    public float DryerTemperature { get { return dryerTemperature; } }
    public float FurnaceTemperature { get { return furnaceTemperature; } }
    public float MissionElapsed { get { return missionElapsed; } }
    public string LastAction { get { return lastAction; } }
    public string AIMessage { get { return aiMessage; } }

    public string CurrentMission
    {
        get { return missionNames[Mathf.Clamp(missionIndex, 0, missionNames.Length - 1)]; }
    }

    public string CurrentMissionDescription
    {
        get { return missionDescriptions[Mathf.Clamp(missionIndex, 0, missionDescriptions.Length - 1)]; }
    }

    public float AccountingDifference
    {
        get { return openingStock + receipts - dispatches - losses - TotalStock; }
    }

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        bins.Clear();
        machines.Clear();
        actions.Clear();
        alarms.Clear();

        bins.Add(new Bin("Receiving Port", 500f, 250f));
        bins.Add(new Bin("Holding Bin", 250f, 120f));

        float[] fills = { 72, 54, 88, 31, 63, 47, 79 };

        for (int i = 0; i < 7; i++)
        {
            Bin silo = new Bin("Silo " + (i + 1), 1000f, fills[i] * 10f);
            silo.moisture = i == 2 ? 12.8f : 13.2f + (i % 3) * 0.3f;
            silo.temperature = 19f + (i % 4);
            silo.impurities = 0.4f + (i % 3) * 0.1f;
            bins.Add(silo);
        }

        bins.Add(new Bin("Dryer", 150f, 0f));
        bins.Add(new Bin("Dispatch Bin", 300f, 0f));

        string[] machineNames =
        {
            "Receiving Conveyor",
            "Bucket Elevator",
            "Transfer Conveyor A",
            "Transfer Conveyor B",
            "Dryer",
            "Furnace",
            "Aeration Fans",
            "Dispatch Conveyor",
            "Loadout Gate"
        };

        foreach (string machineName in machineNames)
            machines.Add(new Machine(machineName));

        machines[8].enabled = false;

        openingStock = CalculateStock();
        receipts = 0f;
        dispatches = 0f;
        losses = 0f;
        totalMoved = 0f;
        missionElapsed = 0f;
        xp = 0;
        qualityScreenCompleted = false;
        activeFault = Fault.None;
        emergencyStop = false;
        safetyInterlock = false;
        gateAligned = true;
        transferRunning = false;
        plantRunning = startPlantOnLaunch;
        initialized = true;

        LogAction("Simulator initialized. Demo stock baseline recorded.");
        ReconcileStock();
    }

    private void Update()
    {
        if (!initialized)
            return;

        float dt = Time.deltaTime * Mathf.Clamp(simulationSpeed, 0.1f, 10f);
        elapsed += dt;

        if (plantRunning && !emergencyStop)
        {
            UpdateMachineTemperatures(dt);
            ProcessDryer(dt);
        }

        if (transferRunning)
            TickTransfer(dt);

        ReconcileStock();
    }

    private void UpdateMachineTemperatures(float dt)
    {
        foreach (Machine machine in machines)
        {
            if (machine.enabled && !machine.faulted)
            {
                machine.temperature = Mathf.Clamp(
                    machine.temperature + Mathf.Sin(elapsed * 0.2f) * dt * 0.02f,
                    20f, 95f);
            }
        }

        dryerTemperature = Mathf.MoveTowards(
            dryerTemperature,
            dryerSetpoint,
            dt * 2f);

        furnaceTemperature = Mathf.MoveTowards(
            furnaceTemperature,
            furnaceSetpoint,
            dt * 3f);
    }

    private void ProcessDryer(float dt)
    {
        Bin dryer = bins[(int)Node.Dryer];

        if (dryer.tonnes <= 0f ||
            activeFault == Fault.DryerOverheat ||
            !machines[4].enabled ||
            !machines[5].enabled)
            return;

        if (dryerSetpoint > 80f || furnaceSetpoint > 220f)
        {
            InjectFault(Fault.DryerOverheat);
            return;
        }

        float processed = Mathf.Min(
            dryer.tonnes,
            dryerCapacity * dt / 3600f);

        if (processed <= 0f)
            return;

        // Illustrative model: the simulation removes water, not dry grain solids.
        // This simplified approximation is for gameplay, not process engineering.
        float oldMoisture = Mathf.Clamp(dryer.moisture, 1f, 60f);
        float newMoisture = Mathf.Max(
            8f,
            oldMoisture - moistureRemovalPerHour * dt / 3600f);

        dryer.moisture = newMoisture;
        dryer.temperature = dryerTemperature;

        float removedWater = processed *
            (oldMoisture - newMoisture) / 100f;

        dryer.tonnes = Mathf.Max(0f, dryer.tonnes - removedWater);
        losses += removedWater;
    }

    private void TickTransfer(float dt)
    {
        if (!plantRunning || emergencyStop || safetyInterlock ||
            activeFault != Fault.None || !gateAligned)
        {
            StopTransfer("Safety condition detected.");
            return;
        }

        if (!RouteEquipmentReady(source, destination))
        {
            StopTransfer("Required route equipment is unavailable.");
            return;
        }

        float amount = Mathf.Min(
            transferRate * dt / 3600f,
            0.5f);

        if (amount <= 0f)
            return;

        string reason;

        if (!CanTransfer(source, destination, amount, out reason))
        {
            StopTransfer(reason);
            return;
        }

        MoveGrain(source, destination, amount);
        totalMoved += amount;
        throughput = transferRate;
        missionElapsed += dt;
    }

    public Bin GetBin(Node node)
    {
        return bins[(int)node];
    }

    public bool CanTransfer(
        Node from, Node to, float amount, out string reason)
    {
        reason = "";

        if (from == to)
        {
            reason = "Select different source and destination nodes.";
            return false;
        }

        if (amount <= 0f)
        {
            reason = "Transfer amount must be positive.";
            return false;
        }

        Bin src = bins[(int)from];
        Bin dst = bins[(int)to];

        if (src.tonnes + 0.0001f < amount)
        {
            reason = "Insufficient grain in " + src.name + ".";
            return false;
        }

        if (dst.Free + 0.0001f < amount)
        {
            reason = dst.name + " does not have sufficient headroom.";
            return false;
        }

        if (to == Node.Dryer && from == Node.Dispatch)
        {
            reason = "Dispatch-to-dryer routing is not permitted.";
            return false;
        }

        if (from == Node.Dryer && to == Node.Receiving)
        {
            reason = "Dryer-to-receiving routing is not permitted.";
            return false;
        }

        if (from == Node.Dispatch)
        {
            reason = "Use the truck dispatch control to remove outgoing stock.";
            return false;
        }

        if (to == Node.Receiving)
        {
            reason = "Use the receiving control to accept incoming grain.";
            return false;
        }

        if (from == Node.Dryer && to == Node.Dryer)
        {
            reason = "Dryer source and destination cannot be identical.";
            return false;
        }

        return true;
    }

    public bool StartTransfer()
    {
        string reason;

        if (!plantRunning || emergencyStop)
        {
            aiMessage = "Start the plant and reset the emergency stop first.";
            return false;
        }

        if (activeFault != Fault.None || safetyInterlock || !gateAligned)
        {
            aiMessage = "Transfer inhibited by a fault or safety interlock.";
            return false;
        }

        if (!RouteEquipmentReady(source, destination))
        {
            aiMessage = "One or more required route machines are unavailable.";
            return false;
        }

        if (!CanTransfer(source, destination, 0.1f, out reason))
        {
            aiMessage = reason;
            return false;
        }

        transferRunning = true;
        LogAction("Transfer started: " + source + " -> " + destination);
        aiMessage = "Route validated. Monitoring simulated grain flow.";
        return true;
    }

    public void StopTransfer(string reason = "Operator stop.")
    {
        if (transferRunning)
            LogAction("Transfer stopped. " + reason);

        transferRunning = false;
        throughput = 0f;
    }

    private bool RouteEquipmentReady(Node from, Node to)
    {
        if (!machines[0].enabled || machines[0].faulted)
            return false;

        if ((int)from >= 2 && (int)from <= 8 ||
            (int)to >= 2 && (int)to <= 8 ||
            from == Node.Dryer || to == Node.Dryer ||
            to == Node.Dispatch)
        {
            if (!machines[1].enabled || machines[1].faulted)
                return false;
        }

        if (from == Node.Dryer || to == Node.Dryer)
        {
            if (!machines[4].enabled || machines[4].faulted ||
                !machines[5].enabled || machines[5].faulted)
                return false;
        }

        if (to == Node.Dispatch)
        {
            if (!machines[7].enabled || machines[7].faulted ||
                !machines[8].enabled || machines[8].faulted)
                return false;
        }

        return true;
    }

    private void MoveGrain(Node from, Node to, float amount)
    {
        Bin src = bins[(int)from];
        Bin dst = bins[(int)to];

        float sourceMoisture = src.moisture;
        float sourceTemperature = src.temperature;
        float sourceImpurities = src.impurities;

        src.tonnes = Mathf.Max(0f, src.tonnes - amount);
        dst.tonnes = Mathf.Min(dst.capacity, dst.tonnes + amount);

        // Destination grain quality is weighted by the mass being mixed.
        float previousMass = Mathf.Max(0f, dst.tonnes - amount);
        float totalMass = previousMass + amount;

        if (totalMass > 0f)
        {
            dst.moisture = (dst.moisture * previousMass +
                            sourceMoisture * amount) / totalMass;

            dst.temperature = (dst.temperature * previousMass +
                               sourceTemperature * amount) / totalMass;

            dst.impurities = (dst.impurities * previousMass +
                              sourceImpurities * amount) / totalMass;
        }

        LogAction("Moved " + amount.ToString("F3") + " t: " +
                  src.name + " -> " + dst.name);
    }

    public bool ReceiveGrain(float tonnes)
    {
        if (!plantRunning || emergencyStop || activeFault != Fault.None)
        {
            aiMessage = "Receiving unavailable: plant or safety state is not ready.";
            return false;
        }

        Bin receiving = bins[(int)Node.Receiving];

        if (tonnes <= 0f || receiving.Free < tonnes)
        {
            aiMessage = "Receiving port capacity is insufficient.";
            return false;
        }

        receiving.tonnes += tonnes;
        receiving.moisture = moistureInput;
        receiving.temperature = 20f;
        receipts += tonnes;

        LogAction("Received external grain: " + tonnes.ToString("F1") + " t.");
        aiMessage = "Incoming batch recorded. Check moisture and route capacity.";
        ReconcileStock();
        return true;
    }

    public bool DispatchTruck(float tonnes)
    {
        Bin dispatch = bins[(int)Node.Dispatch];

        if (!plantRunning || emergencyStop || activeFault != Fault.None ||
            safetyInterlock)
        {
            aiMessage = "Dispatch blocked by plant state or safety interlock.";
            return false;
        }

        if (tonnes <= 0f || dispatch.tonnes < tonnes)
        {
            aiMessage = "Insufficient stock in dispatch bin.";
            return false;
        }

        if (!machines[7].enabled || machines[7].faulted ||
            !machines[8].enabled || machines[8].faulted)
        {
            aiMessage = "Dispatch conveyor or loadout gate is unavailable.";
            return false;
        }

        dispatch.tonnes -= tonnes;
        dispatches += tonnes;

        LogAction("Truck dispatched: " + tonnes.ToString("F1") + " t.");
        ReconcileStock();
        return true;
    }

    public void SetGateAligned(bool aligned)
    {
        gateAligned = aligned;

        if (!aligned)
        {
            safetyInterlock = true;
            StopTransfer("Gate mismatch.");
            LogAlarm("Diverter gate position mismatch.");
            aiMessage = "Gate mismatch. Transfer inhibited.";
        }
        else
        {
            safetyInterlock = activeFault != Fault.None;
            LogAction("Gate alignment confirmed.");
        }
    }

    public void SetPlantRunning(bool running)
    {
        if (running && (emergencyStop || activeFault != Fault.None ||
                        safetyInterlock))
        {
            aiMessage = "Cannot start. Resolve the safety conditions first.";
            return;
        }

        plantRunning = running;

        if (!running)
            StopTransfer("Plant stopped by operator.");

        LogAction(running ? "Plant started." : "Plant stopped.");
    }

    public void ToggleEmergencyStop()
    {
        if (!emergencyStop)
        {
            emergencyStop = true;
            plantRunning = false;
            safetyInterlock = true;
            StopTransfer("Emergency stop activated.");
            LogAlarm("Emergency stop activated.");
            aiMessage = "Emergency stop active. Inspect the simulated plant.";
        }
        else
        {
            emergencyStop = false;
            safetyInterlock = activeFault != Fault.None || !gateAligned;
            LogAction("Emergency stop reset. Check all equipment before restart.");
        }
    }

    public void SetMachineEnabled(int index, bool enabled)
    {
        if (index < 0 || index >= machines.Count)
            return;

        if (machines[index].faulted && enabled)
        {
            aiMessage = "A faulted machine cannot be restarted before fault clearance.";
            return;
        }

        machines[index].enabled = enabled;
        LogAction(machines[index].name +
                  (enabled ? " enabled." : " stopped."));

        if (!enabled)
            StopTransfer("Required equipment stopped.");
    }

    public void InjectFault(Fault fault)
    {
        StopTransfer("Fault simulation.");

        foreach (Machine machine in machines)
        {
            machine.faulted = false;
            machine.temperature = 35f;
        }

        activeFault = fault;

        switch (fault)
        {
            case Fault.ConveyorBlockage:
                machines[2].faulted = true;
                break;

            case Fault.BeltSlip:
                machines[0].faulted = true;
                break;

            case Fault.LevelSensor:
                break;

            case Fault.ElevatorBearing:
                machines[1].faulted = true;
                machines[1].temperature = 92f;
                break;

            case Fault.DryerOverheat:
                machines[4].faulted = true;
                machines[5].faulted = true;
                dryerTemperature = 95f;
                break;

            case Fault.GateMismatch:
                gateAligned = false;
                break;

            case Fault.None:
                gateAligned = true;
                break;
        }

        safetyInterlock = fault != Fault.None || !gateAligned;

        if (fault == Fault.None)
        {
            foreach (Machine machine in machines)
            {
                machine.faulted = false;
                machine.temperature = 35f;
            }
        }

        LogAlarm(fault == Fault.None
            ? "Simulated fault cleared after inspection."
            : "Training fault injected: " + fault);

        DiagnoseFault();
    }

    public void DiagnoseFault()
    {
        switch (activeFault)
        {
            case Fault.None:
                aiMessage = "No active simulated fault. Monitor flow, levels and quality.";
                break;

            case Fault.ConveyorBlockage:
                aiMessage = "BLOCKAGE SUSPECTED: Stop feed, compare flow indications and " +
                    "isolate simulated equipment before inspection.";
                break;

            case Fault.BeltSlip:
                aiMessage = "BELT SLIP SUSPECTED: Compare commanded and measured speed " +
                    "and inspect simulated drive feedback.";
                break;

            case Fault.LevelSensor:
                aiMessage = "LEVEL SENSOR DISAGREEMENT: Cross-check an independent " +
                    "level or weight measurement before filling.";
                break;

            case Fault.ElevatorBearing:
                aiMessage = "BEARING OVERHEAT: Stop the simulated elevator and investigate " +
                    "lubrication, alignment and bearing condition.";
                break;

            case Fault.DryerOverheat:
                aiMessage = "DRYER/FURNACE OVER-TEMPERATURE: Simulated drying is inhibited. " +
                    "Never bypass real burner safety trips.";
                break;

            case Fault.GateMismatch:
                aiMessage = "GATE MISMATCH: Requested route and feedback disagree. " +
                    "Keep transfer stopped until position is verified.";
                break;
        }

        safetyInterlock = activeFault != Fault.None || !gateAligned;
    }

    public void RunQualityScreen()
    {
        qualityScreenCompleted = true;

        qualityScore = Mathf.Clamp(
            100f - Mathf.Max(0f, moistureInput - 13f) * 1.4f -
            (activeFault == Fault.DryerOverheat ? 12f : 0f),
            40f, 100f);

        aiMessage = moistureInput > 14f
            ? "Incoming moisture flagged for review. Confirm grain type and approved specification."
            : "Simplified quality screen passed. Continue representative sampling.";

        LogAction("Grain quality screening completed.");
    }

    public void BuildAIRecommendation()
    {
        if (activeFault != Fault.None)
        {
            DiagnoseFault();
            return;
        }

        Bin fullest = bins[2];

        for (int i = 3; i <= 8; i++)
        {
            if (bins[i].Fill > fullest.Fill)
                fullest = bins[i];
        }

        if (fullest.Fill >= 0.85f)
        {
            aiMessage = fullest.name + " is " +
                (fullest.Fill * 100f).ToString("F0") +
                "% full. Choose a silo with more available capacity.";
        }
        else if (moistureInput > 14f)
        {
            aiMessage = "Incoming moisture is elevated in this training model. " +
                "Review grain-specific drying requirements before storage.";
        }
        else
        {
            aiMessage = "No active simulated fault. Validate route, headroom, " +
                "gate feedback, quality and inventory reconciliation.";
        }
    }

    public void NextMission(int direction)
    {
        missionIndex = (missionIndex + direction + missionNames.Length) %
                       missionNames.Length;
    }

    public bool ScoreMission()
    {
        bool passed = false;

        switch (missionIndex)
        {
            case 0:
                passed = receipts > 0f &&
                    bins[(int)Node.Holding].tonnes > 0f;
                break;

            case 1:
                passed = totalMoved > 0f &&
                    Mathf.Abs(AccountingDifference) < 0.05f;
                break;

            case 2:
                passed = activeFault == Fault.None &&
                    alarms.Count > 0;
                break;

            case 3:
                passed = qualityScreenCompleted;
                break;

            case 4:
                passed = dispatches > 0f &&
                    Mathf.Abs(AccountingDifference) < 0.05f;
                break;
        }

        if (passed)
        {
            xp += 100;
            credits += 25;
            LogAction("Mission passed: " + CurrentMission);
            aiMessage = "Mission passed! +100 XP and +25 credits.";
        }
        else
        {
            LogAction("Mission not completed: " + CurrentMission);
            aiMessage = "Mission incomplete. Follow the stated objective and try again.";
        }

        return passed;
    }

    public void ResetDemoInventory()
    {
        StopTransfer("Inventory reset.");

        bins[(int)Node.Receiving].tonnes = 250f;
        bins[(int)Node.Holding].tonnes = 120f;

        float[] fills = { 72, 54, 88, 31, 63, 47, 79 };

        for (int i = 0; i < 7; i++)
            bins[i + 2].tonnes = fills[i] * 10f;

        bins[(int)Node.Dryer].tonnes = 0f;
        bins[(int)Node.Dispatch].tonnes = 0f;

        foreach (Bin bin in bins)
        {
            bin.moisture = 13f;
            bin.temperature = 20f;
            bin.impurities = 0.5f;
        }

        openingStock = CalculateStock();
        receipts = 0f;
        dispatches = 0f;
        losses = 0f;
        totalMoved = 0f;

        ReconcileStock();
        LogAction("Demo inventory reset; new opening balance recorded.");
    }

    public void ExportReport()
    {
        Debug.Log(
            "GRAIN SILO REPORT\n" +
            "Stock: " + TotalStock.ToString("F2") + " t\n" +
            "Opening: " + openingStock.ToString("F2") + " t\n" +
            "Receipts: " + receipts.ToString("F2") + " t\n" +
            "Dispatches: " + dispatches.ToString("F2") + " t\n" +
            "Losses: " + losses.ToString("F2") + " t\n" +
            "Accounting difference: " + AccountingDifference.ToString("F4") + " t\n" +
            "Fault: " + activeFault);

        LogAction("Inventory report exported to Unity Console.");
    }

    private float CalculateStock()
    {
        float total = 0f;

        foreach (Bin bin in bins)
            total += bin.tonnes;

        return total;
    }

    private void ReconcileStock()
    {
        TotalStock = CalculateStock();
    }

    private void LogAction(string message)
    {
        lastAction = message;
        actions.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + message);

        if (actions.Count > 100)
            actions.RemoveAt(actions.Count - 1);
    }

    private void LogAlarm(string message)
    {
        alarms.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + message);

        if (alarms.Count > 100)
            alarms.RemoveAt(alarms.Count - 1);
    }
}
