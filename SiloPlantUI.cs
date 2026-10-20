
using UnityEngine;

public class SiloPlantUI : MonoBehaviour
{
    public SiloPlantSimulator simulator;

    private int activeTab;
    private Vector2 actionScroll;
    private Vector2 alarmScroll;

    private GUIStyle title;
    private GUIStyle heading;
    private GUIStyle body;
    private GUIStyle small;
    private GUIStyle button;
    private GUIStyle panel;
    private GUIStyle warning;
    private GUIStyle good;

    private Texture2D backgroundTex;
    private Texture2D headerTex;
    private Texture2D panelTex;
    private Texture2D barTex;

    private readonly string[] tabs =
    {
        "Plant Map", "Controls", "Inventory",
        "Quality", "Reports", "Missions"
    };

    private void Start()
    {
        BuildStyles();
        FindSimulator();
    }

    private void FindSimulator()
    {
        if (simulator == null)
            simulator = FindObjectOfType<SiloPlantSimulator>();
    }

    private Texture2D MakeTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private void BuildStyles()
    {
        backgroundTex = MakeTexture(new Color(0.025f, 0.10f, 0.065f));
        headerTex = MakeTexture(new Color(0.15f, 0.48f, 0.20f));
        panelTex = MakeTexture(new Color(0.045f, 0.17f, 0.11f));
        barTex = MakeTexture(new Color(0.36f, 0.78f, 0.28f));

        title = new GUIStyle(GUI.skin.label);
        title.fontSize = 22;
        title.fontStyle = FontStyle.Bold;
        title.normal.textColor = new Color(0.78f, 0.96f, 0.70f);

        heading = new GUIStyle(GUI.skin.label);
        heading.fontSize = 15;
        heading.fontStyle = FontStyle.Bold;
        heading.normal.textColor = new Color(0.78f, 0.93f, 0.75f);

        body = new GUIStyle(GUI.skin.label);
        body.fontSize = 12;
        body.wordWrap = true;
        body.normal.textColor = Color.white;

        small = new GUIStyle(body);
        small.fontSize = 10;
        small.normal.textColor = new Color(0.72f, 0.84f, 0.75f);

        button = new GUIStyle(GUI.skin.button);
        button.fontSize = 11;
        button.fontStyle = FontStyle.Bold;
        button.wordWrap = true;
        button.fixedHeight = 32;
        button.normal.textColor = Color.white;

        panel = new GUIStyle(GUI.skin.box);
        panel.normal.background = panelTex;
        panel.padding = new RectOffset(10, 10, 8, 8);

        warning = new GUIStyle(body);
        warning.fontStyle = FontStyle.Bold;
        warning.normal.textColor = new Color(1f, 0.58f, 0.32f);

        good = new GUIStyle(body);
        good.fontStyle = FontStyle.Bold;
        good.normal.textColor = new Color(0.55f, 0.94f, 0.49f);
    }

    private void OnGUI()
    {
        if (title == null)
            BuildStyles();

        if (simulator == null)
            FindSimulator();

        if (simulator == null || !simulator.IsInitialized)
        {
            GUI.Label(new Rect(20, 20, 600, 40),
                "Grain Silo Simulator: waiting for simulation engine.");
            return;
        }

        float scale = Mathf.Clamp(Screen.width / 1280f, 0.55f, 1f);
        Matrix4x4 oldMatrix = GUI.matrix;

        GUI.matrix = Matrix4x4.TRS(
            Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        float width = Screen.width / scale;
        float height = Screen.height / scale;

        GUI.DrawTexture(new Rect(0, 0, width, height), backgroundTex);
        GUI.DrawTexture(new Rect(0, 0, width, 68), headerTex);

        GUI.Label(new Rect(18, 7, 720, 30),
            "AI GRAIN SILO PLANT SIMULATOR", title);

        GUI.Label(new Rect(20, 40, 620, 18),
            "LEARN  |  OPERATE  |  MONITOR  |  TROUBLESHOOT", small);

        GUI.Label(new Rect(width - 300, 20, 280, 25),
            simulator.EmergencyStop ? "EMERGENCY STOP" :
            simulator.PlantRunning ? "PLANT RUNNING" : "PLANT STOPPED",
            simulator.EmergencyStop ? warning :
            simulator.PlantRunning ? good : warning);

        for (int i = 0; i < tabs.Length; i++)
        {
            if (GUI.Button(new Rect(18 + i * 116, 76, 110, 30),
                tabs[i], button))
                activeTab = i;
        }

        float leftWidth = Mathf.Max(220, width * 0.20f);
        float rightWidth = Mathf.Max(250, width * 0.23f);
        float mainX = leftWidth + 28;
        float mainWidth = width - leftWidth - rightWidth - 46;
        float top = 116;
        float bottom = height - 25;
        float contentHeight = bottom - top;

        GUI.Box(new Rect(12, top, leftWidth, contentHeight),
            GUIContent.none, panel);

        GUI.Box(new Rect(mainX, top, mainWidth, contentHeight),
            GUIContent.none, panel);

        GUI.Box(new Rect(width - rightWidth - 12, top,
            rightWidth, contentHeight), GUIContent.none, panel);

        GUILayout.BeginArea(new Rect(22, top + 8,
            leftWidth - 20, contentHeight - 16));
        DrawEquipmentPanel();
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(mainX + 10, top + 8,
            mainWidth - 20, contentHeight - 16));

        switch (activeTab)
        {
            case 0: DrawPlantMap(); break;
            case 1: DrawControls(); break;
            case 2: DrawInventory(); break;
            case 3: DrawQuality(); break;
            case 4: DrawReports(); break;
            case 5: DrawMissions(); break;
        }

        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(width - rightWidth, top + 8,
            rightWidth - 18, contentHeight - 16));
        DrawOverview();
        GUILayout.EndArea();

        GUI.DrawTexture(new Rect(0, height - 17, width, 17), headerTex);
        GUI.Label(new Rect(15, height - 17, width - 30, 17),
            "TRAINING SIMULATION ONLY  |  Verify real equipment using approved procedures.",
            small);

        GUI.matrix = oldMatrix;
    }

    private void DrawEquipmentPanel()
    {
        GUILayout.Label("EQUIPMENT STATUS", heading);
        GUILayout.Space(5);

        foreach (SiloPlantSimulator.Machine machine in simulator.Machines)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(machine.name, small, GUILayout.Width(118));

            GUILayout.Label(machine.faulted ? "FAULT" :
                machine.enabled ? "RUN" : "STOP",
                machine.faulted ? warning :
                machine.enabled ? good : small);

            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);
        GUILayout.Label("SAFETY CONTROLS", heading);

        GUILayout.Label(simulator.SafetyInterlock
            ? "INTERLOCK ACTIVE" : "No active interlock",
            simulator.SafetyInterlock ? warning : good);

        if (GUILayout.Button(simulator.EmergencyStop
            ? "RESET E-STOP" : "EMERGENCY STOP", button))
            simulator.ToggleEmergencyStop();

        if (GUILayout.Button(simulator.PlantRunning
            ? "STOP PLANT" : "START PLANT", button))
            simulator.SetPlantRunning(!simulator.PlantRunning);

        GUILayout.Space(6);
        GUILayout.Label("LAST ACTION", heading);
        GUILayout.Label(simulator.LastAction, small);
    }

    private void DrawOverview()
    {
        GUILayout.Label("PLANT OVERVIEW", heading);
        GUILayout.Label("Current stock", small);
        GUILayout.Label(simulator.TotalStock.ToString("N1") + " t", title);

        GUILayout.Label("Transfer rate", small);
        GUILayout.Label((simulator.TransferRunning
            ? simulator.Throughput : 0f).ToString("N0") + " t/h", heading);

        GUILayout.Label("Quality score", small);
        GUILayout.Label(simulator.QualityScore.ToString("N0") + " / 100", heading);

        GUILayout.Label("Accounting difference", small);
        float difference = simulator.AccountingDifference;

        GUILayout.Label(difference.ToString("+0.000;-0.000;0.000") + " t",
            Mathf.Abs(difference) < 0.05f ? good : warning);

        GUILayout.Space(8);
        GUILayout.Label("SILO LEVELS", heading);

        for (int i = 2; i <= 8; i++)
        {
            SiloPlantSimulator.Bin silo =
                simulator.Bins[i];

            GUILayout.Label(silo.name + "  " +
                (silo.Fill * 100f).ToString("F0") + "%", small);

            Rect rect = GUILayoutUtility.GetRect(
                100, 12, GUILayout.ExpandWidth(true));

            GUI.DrawTexture(rect, backgroundTex);

            GUI.color = silo.Fill >= 0.85f
                ? new Color(0.9f, 0.55f, 0.15f) : Color.green;

            GUI.DrawTexture(new Rect(rect.x, rect.y,
                rect.width * Mathf.Clamp01(silo.Fill), rect.height), barTex);

            GUI.color = Color.white;
        }

        GUILayout.Space(8);
        GUILayout.Label("LOCAL AI ADVISOR", heading);
        GUILayout.Label(simulator.AIMessage, body);

        if (GUILayout.Button("GENERATE RECOMMENDATION", button))
            simulator.BuildAIRecommendation();

        if (simulator.ActiveFault != SiloPlantSimulator.Fault.None &&
            GUILayout.Button("CLEAR TRAINING FAULT", button))
            simulator.InjectFault(SiloPlantSimulator.Fault.None);
    }

    private void DrawPlantMap()
    {
        GUILayout.Label("PLANT MAP", title);
        GUILayout.Label("Simulated process inventory", small);
        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        DrawBinCard(SiloPlantSimulator.Node.Receiving);
        DrawBinCard(SiloPlantSimulator.Node.Holding);
        GUILayout.EndHorizontal();

        GUILayout.Label("STORAGE SILOS", heading);
        GUILayout.BeginHorizontal();

        for (int i = 2; i <= 8; i++)
            DrawBinCard((SiloPlantSimulator.Node)i);

        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        DrawBinCard(SiloPlantSimulator.Node.Dryer);
        DrawBinCard(SiloPlantSimulator.Node.Dispatch);
        GUILayout.EndHorizontal();

        GUILayout.Space(12);
        GUILayout.Label("PROCESS ROUTE", heading);
        GUILayout.Label(
            "Receiving -> Holding -> Storage silos -> Dryer -> Storage / Dispatch",
            body);

        GUILayout.Label(
            "Select any valid source and destination in Controls. " +
            "Transfers update the central inventory model.",
            small);
    }

    private void DrawBinCard(SiloPlantSimulator.Node node)
    {
        SiloPlantSimulator.Bin bin = simulator.GetBin(node);

        GUILayout.BeginVertical(panel, GUILayout.MinWidth(60),
            GUILayout.ExpandWidth(true));

        GUILayout.Label(bin.name, small);
        GUILayout.Label(bin.tonnes.ToString("F0") + " t", heading);
        GUILayout.Label((bin.Fill * 100f).ToString("F0") + "% full", small);

        GUILayout.EndVertical();
    }

    private void DrawControls()
    {
        GUILayout.Label("OPERATOR CONTROLS", title);
        GUILayout.Label("Choose a route and validate it before moving grain.", small);
        GUILayout.Space(8);

        GUILayout.Label("Source", heading);

        int source = (int)simulator.source;
        source = GUILayout.SelectionGrid(source,
            simulator.NodeNames as string[] ?? GetNodeNames(), 3, button);

        simulator.source = (SiloPlantSimulator.Node)source;

        GUILayout.Space(8);
        GUILayout.Label("Destination", heading);

        int destination = (int)simulator.destination;
        destination = GUILayout.SelectionGrid(destination,
            GetNodeNames(), 3, button);

        simulator.destination = (SiloPlantSimulator.Node)destination;

        GUILayout.Space(8);
        GUILayout.Label("Transfer rate: " +
            simulator.transferRate.ToString("F0") + " t/h", body);

        simulator.transferRate = GUILayout.HorizontalSlider(
            simulator.transferRate, 10f, 120f);

        GUILayout.Label("Incoming moisture: " +
            simulator.MoistureInput.ToString("F1") + "%", body);

        // Moisture input is updated through the public method below.
        moistureSlider();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("START TRANSFER", button))
            simulator.StartTransfer();

        if (GUILayout.Button("STOP TRANSFER", button))
            simulator.StopTransfer("Operator stop.");

        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("RECEIVING & DISPATCH", heading);

        if (GUILayout.Button("RECEIVE 50 t", button))
            simulator.ReceiveGrain(50f);

        if (GUILayout.Button("DISPATCH 20 t", button))
            simulator.DispatchTruck(20f);

        GUILayout.Space(8);
        GUILayout.Label("DIVERTER GATE", heading);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("ALIGN GATE", button))
            simulator.SetGateAligned(true);

        if (GUILayout.Button("SIMULATE MISMATCH", button))
            simulator.InjectFault(SiloPlantSimulator.Fault.GateMismatch);

        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label("TRAINING FAULTS", heading);

        if (GUILayout.Button("Conveyor blockage", button))
            simulator.InjectFault(SiloPlantSimulator.Fault.ConveyorBlockage);

        if (GUILayout.Button("Belt slip", button))
            simulator.InjectFault(SiloPlantSimulator.Fault.BeltSlip);

        if (GUILayout.Button("Level sensor", button))
            simulator.InjectFault(SiloPlantSimulator.Fault.LevelSensor);

        if (GUILayout.Button("Elevator bearing", button))
            simulator.InjectFault(SiloPlantSimulator.Fault.ElevatorBearing);

        if (GUILayout.Button("Dryer overheat", button))
            simulator.InjectFault(SiloPlantSimulator.Fault.DryerOverheat);

        GUILayout.Space(8);
        GUILayout.Label("DRYER / FURNACE", heading);

        GUILayout.Label("Dryer setpoint: " +
            simulator.dryerSetpoint.ToString("F0") + " °C", body);

        simulator.dryerSetpoint = GUILayout.HorizontalSlider(
            simulator.dryerSetpoint, 35f, 90f);

        GUILayout.Label("Furnace setpoint: " +
            simulator.furnaceSetpoint.ToString("F0") + " °C", body);

        simulator.furnaceSetpoint = GUILayout.HorizontalSlider(
            simulator.furnaceSetpoint, 80f, 240f);

        if (GUILayout.Button("APPLY SETPOINTS", button))
        {
            if (simulator.dryerSetpoint > 80f ||
                simulator.furnaceSetpoint > 220f)
                simulator.InjectFault(SiloPlantSimulator.Fault.DryerOverheat);
            else
                simulator.BuildAIRecommendation();
        }
    }

    private void moistureSlider()
    {
        // Use the public setter so the UI can adjust incoming moisture.
        simulator.SetMoistureInput(
            GUILayout.HorizontalSlider(simulator.MoistureInput, 8f, 30f));
    }

    private string[] GetNodeNames()
    {
        string[] names = new string[simulator.NodeNames.Count];

        for (int i = 0; i < names.Length; i++)
            names[i] = simulator.NodeNames[i];

        return names;
    }

    private void DrawInventory()
    {
        GUILayout.Label("INVENTORY & TONNAGE", title);
        GUILayout.Space(8);

        GUILayout.Label("Opening stock: " +
            simulator.OpeningStock.ToString("N1") + " t", body);

        GUILayout.Label("Receipts: " +
            simulator.Receipts.ToString("N1") + " t", body);

        GUILayout.Label("Dispatches: " +
            simulator.Dispatches.ToString("N1") + " t", body);

        GUILayout.Label("Recorded losses: " +
            simulator.Losses.ToString("N2") + " t", body);

        GUILayout.Label("Current stock: " +
            simulator.TotalStock.ToString("N1") + " t", heading);

        GUILayout.Label("Total transfer movement: " +
            simulator.TotalMoved.ToString("N2") + " t", body);

        GUILayout.Label("Mass-balance difference: " +
            simulator.AccountingDifference.ToString("F4") + " t",
            Mathf.Abs(simulator.AccountingDifference) < 0.05f
                ? good : warning);

        GUILayout.Space(10);

        if (GUILayout.Button("RESET DEMO INVENTORY", button))
            simulator.ResetDemoInventory();

        GUILayout.Space(8);
        GUILayout.Label("BIN INVENTORY", heading);

        foreach (SiloPlantSimulator.Bin bin in simulator.Bins)
        {
            GUILayout.Label(bin.name + ": " +
                bin.tonnes.ToString("N1") + " / " +
                bin.capacity.ToString("N0") + " t", body);
        }
    }

    private void DrawQuality()
    {
        GUILayout.Label("GRAIN QUALITY LAB", title);

        GUILayout.Label(
            "Readings are illustrative and must not replace grain sampling.",
            small);

        GUILayout.Space(8);

        for (int i = 2; i <= 8; i++)
        {
            SiloPlantSimulator.Bin bin = simulator.Bins[i];

            GUILayout.BeginVertical(panel);
            GUILayout.Label(bin.name, heading);
            GUILayout.Label("Moisture: " + bin.moisture.ToString("F1") + "%", body);
            GUILayout.Label("Temperature: " +
                bin.temperature.ToString("F1") + " °C", body);
            GUILayout.Label("Impurities: " +
                bin.impurities.ToString("F1") + "%", body);
            GUILayout.EndVertical();
        }

        GUILayout.Space(8);

        GUILayout.Label("Incoming moisture: " +
            simulator.MoistureInput.ToString("F1") + "%", body);

        if (GUILayout.Button("RUN QUALITY SCREEN", button))
            simulator.RunQualityScreen();

        GUILayout.Label("Quality score: " +
            simulator.QualityScore.ToString("F0") + " / 100", heading);

        GUILayout.Label(
            "Guidance: sample incoming grain, verify its grade, " +
            "trend moisture and temperature, and investigate hot spots.",
            small);
    }

    private void DrawReports()
    {
        GUILayout.Label("EVENTS & REPORTS", title);

        GUILayout.Label("Recent actions", heading);
        actionScroll = GUILayout.BeginScrollView(
            actionScroll, GUILayout.Height(180));

        foreach (string entry in simulator.Actions)
            GUILayout.Label(entry, small);

        GUILayout.EndScrollView();

        GUILayout.Space(8);
        GUILayout.Label("Alarm history", heading);

        alarmScroll = GUILayout.BeginScrollView(
            alarmScroll, GUILayout.Height(180));

        if (simulator.Alarms.Count == 0)
            GUILayout.Label("No alarms recorded.", good);

        foreach (string entry in simulator.Alarms)
            GUILayout.Label(entry, warning);

        GUILayout.EndScrollView();

        if (GUILayout.Button("EXPORT REPORT TO CONSOLE", button))
            simulator.ExportReport();
    }

    private void DrawMissions()
    {
        GUILayout.Label("LEARNING MISSIONS", title);
        GUILayout.Label(simulator.CurrentMission, heading);
        GUILayout.Label(simulator.CurrentMissionDescription, body);

        GUILayout.Space(8);

        GUILayout.Label("Mission elapsed: " +
            (simulator.MissionElapsed / 60f).ToString("F1") +
            " simulated minutes", body);

        GUILayout.Label("XP: " + simulator.xp +
            " | Credits: " + simulator.credits, body);

        if (GUILayout.Button("PREVIOUS MISSION", button))
            simulator.NextMission(-1);

        if (GUILayout.Button("NEXT MISSION", button))
            simulator.NextMission(1);

        if (GUILayout.Button("COMPLETE / SCORE MISSION", button))
            simulator.ScoreMission();

        GUILayout.Space(10);
        GUILayout.Label("SCORING", heading);

        GUILayout.Label(
            "Safe operation, valid routing, correct inventory, " +
            "grain quality and successful fault diagnosis.",
            body);
    }
}
