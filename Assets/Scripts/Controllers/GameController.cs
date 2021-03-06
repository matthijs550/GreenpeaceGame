﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Timers;
using UnityEngine.UI;
using UnityEngine.Analytics;
using Facebook.Unity;

/*regions order:
0 = noord
1 = oost
2 = west
3 = zuid
*/

public class GameController : MonoBehaviour
{
    public Game game;
    private int taal;

    double score = 0;

    GameObject[] buildingInstances; //noord,oost,west,zuid
    GameObject eventInstance;
    public Button MonthlyReportButon;
    public Button YearlyReportButton;
    public Button CompletedButton;
    private UpdateUI updateUI;
    public GameObject noordNederland;
    public GameObject oostNederland;
    public GameObject westNederland;
    public GameObject zuidNederland;

    public Vector3[] afterActionPosition;

    public GameObject eventObject;
    public GameObject buildingObject;

    public double[] eventConsequenceModifiers;

    // private float time;
    public bool autoSave = true;
    public bool autoEndTurn = false;

    public bool trackingEnabled = true;
    
    float height = Screen.height / (1080 / 55);

    //multiplayer
    public GameObject player;
    public Player playerController;

    #region Awake/Start/Update
    private void Awake()
    {
        eventConsequenceModifiers = new double[5] { 0.8, 0.9, 1, 1.1, 1.2 };
        updateUI = GetComponent<UpdateUI>();

        if (!ApplicationModel.loadGame)
        {
            game = new Game();

            //loads in all the data of the game
            LoadRegions();
            LoadRegionActions();
            LoadBuildings();
            LoadGameEvents();
            LoadQuests();
            LoadBuildings();
            LoadCards();

            //UpdateXMLFiles();

            game.gameStatistics.UpdateRegionalAvgs(game);
            UpdateTimeline();
            UpdateRegionActionAvailability();

            //set reports
            game.monthlyReport.UpdateStatistics(game.regions);
            game.yearlyReport.UpdateStatistics(game.regions);

            //set advisors
            game.economyAdvisor.DetermineDisplayMessage(game.currentYear, game.currentMonth, game.gameStatistics.income);
            game.pollutionAdvisor.DetermineDisplayMessage(game.currentYear, game.currentMonth, game.gameStatistics.pollution);
            game.happinessAnalyst.DetermineDisplayMessage(game.currentYear, game.currentMonth, game.gameStatistics.happiness);
        }
        else
        {
            LoadGame();
        }

        // Instantiate elk event die bezig is en zet het icoontje op de map
        foreach (MapRegion r in game.regions)
        {
            foreach (GameEvent e in r.inProgressGameEvents)
            {
                /*GameObject */eventInstance = GameController.Instantiate(eventObject);
                eventInstance.GetComponent<EventObjectController>().PlaceEventIcons(this, r, e);
            }
        }

        // Geef de Game Class aan UpdateUI
        updateUI.LinkGame(game);
    }

    

    void Start()
    {
        taal = ApplicationModel.language;
        SetPlayerTrackingData();
        autoSave = false;

        // De coroutines die de buttons omhoog brengen op het moment dat ze beschikbaar moeten worden
        StartCoroutine(updateUI.showBtnQuests());
        StartCoroutine(updateUI.showBtnInvestments());
        StartCoroutine(updateUI.showBtnCards());
        StartCoroutine(showBuildingIcons());

        //the positions of the "dropdown" buttons for monthly/yearly report
        afterActionPosition = new Vector3[3];
        afterActionPosition[0] = new Vector3( 5, 5 + height * 2 * 0, 0);
        afterActionPosition[1] = new Vector3( 5, 5 + height * 2 * 1, 0);
        afterActionPosition[2] = new Vector3( 5, 5 + height * 2 * 2, 0); //currently not being used cause there are only 2 buttons using these positions

        // setup Region Controllers
        noordNederland.GetComponent<RegionController>().Init(this, updateUI, game.regions[0]);
        oostNederland.GetComponent<RegionController>().Init(this, updateUI, game.regions[1]);
        westNederland.GetComponent<RegionController>().Init(this, updateUI, game.regions[2]);
        zuidNederland.GetComponent<RegionController>().Init(this, updateUI, game.regions[3]);

        EventManager.ChangeMonth += NextTurn;
        EventManager.SaveGame += SaveGame;
        EventManager.LeaveGame += SetGameplayTrackingData;

        //necessary actions when a multiplayer game is started
        if (ApplicationModel.multiplayer)
        {
            autoSave = false;
            game.ChangeGameForMultiplayer();
            player = PhotonNetwork.Instantiate("PGLPlayer", new Vector3(12, 5, 9), new Quaternion(50, 0, 0, 0), 0);
            player.SetActive(true);
            player.gameObject.SetActive(true);
            playerController = player.GetComponent<Player>();
            updateUI.playerController = playerController;
            SetDelegates();
            game.nextTurnIsclicked = false;
            game.OtherPlayerClickedNextTurn = false;
            game.isWaiting = false;
        }

        //load in the monthly/yearly reports from previous save
        if (ApplicationModel.loadGame)
        {
            if (!(game.currentYear == 1 && game.currentMonth == 1))
                GenerateMonthlyReport(0);
            if (game.currentYear != 1 && game.currentMonth == 1)
                GenerateYearlyReport(1);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // UITZETTEN BIJ EEN BUILD
        /*
        if (((Input.GetKeyDown(KeyCode.Return) || autoEndTurn) && game.currentYear < 31 && game.gameStatistics.pollution > 0 &&
            game.tutorial.tutorialNexTurnPossibe))
        {
            if (!ApplicationModel.multiplayer)
                EventManager.CallChangeMonth();

            else if (!game.nextTurnIsclicked)
            {
                MultiplayerManager.CallNextTurnClick();
            }
        }
        */
        
        // Update the main screen UI (Icons and date)
        updateUIMainScreen();

        // Update the UI in popup screen
        if (updateUI.getPopupActive())
            updateUIPopups();

        /* Update values in Tooltips for Icons in Main UI
        if (updateUI.getTooltipActive())
            updateUITooltips(); */
    }
    #endregion

    #region Coroutines
    private IEnumerator showBuildingIcons()
    {
        if (!ApplicationModel.multiplayer)
        {
            while (game.currentYear < 11)
                yield return null;
        }

        buildingInstances = new GameObject[4] { Instantiate(buildingObject), Instantiate(buildingObject),
                                                Instantiate(buildingObject), Instantiate(buildingObject) };

        for (int i = 0; i < game.regions.Count; i++)
        {
            buildingInstances[i].GetComponent<BuildingObjectController>().placeBuildingIcon(this, game.regions[i], game.regions[i].activeBuilding);
        }

        if (!game.tutorial.tutorialBuildingsDone)
            updateUI.startTutorialBuildings();
    }

    public IEnumerator SetMonthlyReportButtonLocation(Vector3 currentPosition, Vector3 endPosition)
    {
        float positionDiff = currentPosition.y - endPosition.y;
        while (currentPosition.y > endPosition.y)
        {
            currentPosition.y -= positionDiff / 60;
            if (currentPosition.y < endPosition.y)
                currentPosition = endPosition;
            updateUI.btnMonthlyReportStats.gameObject.transform.position = currentPosition;
            yield return new WaitForFixedUpdate();
        }

        updateUI.btnMonthlyReportStats.interactable = true;
    }

    public IEnumerator SetYearlyReportButtonLocation(Vector3 currentPosition, Vector3 endPosition)
    {
        float positionDiff = currentPosition.y - endPosition.y;
        while (currentPosition.y > endPosition.y)
        {
            currentPosition.y -= positionDiff / 60;
            if (currentPosition.y < endPosition.y)
                currentPosition = endPosition;
            updateUI.btnYearlyReportStats.gameObject.transform.position = currentPosition;
            yield return new WaitForFixedUpdate();
        }

        updateUI.btnYearlyReportStats.interactable = true;
    }
    #endregion

    #region Analytics
    private void OnApplicationQuit()
    {

        SetGameplayTrackingData();
    }

    private void OnDestroy()
    {
        EventManager.ChangeMonth -= NextTurn;
        EventManager.SaveGame -= SaveGame;
        EventManager.LeaveGame -= SetGameplayTrackingData;

        if (ApplicationModel.multiplayer)
        {
            MultiplayerManager.NextTurnClicked -= GetOtherPlayerNextTurn;
            MultiplayerManager.NextTurnClick -= ClickedNextTurn;
            MultiplayerManager.ChangeOwnMoney -= SendOwnMoneyChange;
            MultiplayerManager.ChangeOtherPlayerMoney -= game.gameStatistics.ModifyMoneyOtherPlayer;
            MultiplayerManager.StartAction -= StartOtherPlayerAction;
            MultiplayerManager.StartEvent -= GetOtherPlayerEvent;
            MultiplayerManager.PickEventChoice -= StartOtherPlayerEventChoice;
            MultiplayerManager.PlayCard -= StartOtherPlayerCard;
            MultiplayerManager.Invest -= GetOtherPlayerInvestment;
            MultiplayerManager.MakeBuilding -= GetOtherPlayerBuilding;
            MultiplayerManager.UpdateChat -= updateUI.updateChatMessages;
            MultiplayerManager.UpdateLogMessage -= updateUI.SetRemotePlayerText;
            MultiplayerManager.UpdateActivityLog -= updateUI.UpdateActivityLogText;
        }
    }

    public void SetPlayerTrackingData()
    {
        if (trackingEnabled)
        {
            Analytics.CustomEvent("PlayerData", new Dictionary<string, object>
            {
                { "UserID", SystemInfo.deviceUniqueIdentifier },
                { "OperatingSystem", SystemInfo.operatingSystem },
                { "DeviceModel", SystemInfo.deviceModel },
                { "DeviceName", SystemInfo.deviceName },
                { "DeviceType", SystemInfo.deviceType },
            });
        }
    }
    public void SetScoreTrackingData(double score)
    {
        if (trackingEnabled)
        {
            Analytics.CustomEvent("GameCompletionScore", new Dictionary<string, object>
            {
                { "Score", score.ToString("0") },
                { "Year", game.currentYear.ToString() },
                { "Month", game.currentMonth.ToString() },
                { "Pollution", game.gameStatistics.pollution.ToString("0.00") },
                { "Money", game.gameStatistics.money.ToString("0") },
                { "Income", game.gameStatistics.income.ToString("0") },
                { "Happiness", game.gameStatistics.happiness.ToString("0.00") },
                { "EcoAwareness", game.gameStatistics.ecoAwareness.ToString("0.00") },
                { "Prosperity", game.gameStatistics.prosperity.ToString("0.00") },
                { "TimePlayed", game.totalTimePlayed.ToString() }
            });
        }
    }

    public void SetGameplayTrackingData()
    {
        game.totalTimePlayed += Time.timeSinceLevelLoad;

        if (trackingEnabled)
        {
            int totalMonths = game.currentMonth + game.currentYear * 12;
            Analytics.CustomEvent("GameStatisticsData", new Dictionary<string, object>
            {
                { "TotalMonths", totalMonths.ToString() },
                { "Pollution", game.gameStatistics.pollution.ToString("0.00") },
                { "Money", game.gameStatistics.money.ToString("0") },
                { "Income", game.gameStatistics.income.ToString("0") },
                { "Happiness", game.gameStatistics.happiness.ToString("0.00") },
                { "EcoAwareness", game.gameStatistics.ecoAwareness.ToString("0.00") },
                { "Prosperity", game.gameStatistics.prosperity.ToString("0.00") },
                { "TimePlayed", Time.timeSinceLevelLoad.ToString("0") },
                { "TotalTimePlayed", game.totalTimePlayed.ToString() }
            });
        }
    }

    public void SetYearlyTrackingData()
    {
        if (trackingEnabled)
        {
            SetYearlyStatistics();
            SetYearlyCompletedFeatures();
        }
    }

    public void SetYearlyStatistics()
    {
        Analytics.CustomEvent("Year" + game.currentYear + "StartGameStatisticsData", new Dictionary<string, object>
        {
            { "Pollution", game.gameStatistics.pollution.ToString("0.00") },
            { "Money", game.gameStatistics.money.ToString("0") },
            { "Income", game.gameStatistics.income.ToString("0") },
            { "Happiness", game.gameStatistics.happiness.ToString("0.00") },
            { "EcoAwareness", game.gameStatistics.ecoAwareness.ToString("0.00") },
            { "Prosperity", game.gameStatistics.prosperity.ToString("0.00") },
            { "TimePlayed", game.totalTimePlayed.ToString() }
        });
    }

    public void SetYearlyCompletedFeatures()
    {
        Analytics.CustomEvent("Year" + game.currentYear + "StartCompletedFeaturesData", new Dictionary<string, object>
        {
            { "CompletedEventsCount", game.completedEventsCount.ToString() },
            { "AbandonedEventsCount", game.abandonedEventsCount.ToString() },
            { "CompletedActionsCount", game.completedActionsCount.ToString() },
            { "CompletedQuestsCount", game.completedQuestsCount.ToString() },
            { "ReceivedCardsCount", game.receivedCardsCount.ToString() },
        });

    }
    #endregion

    #region Facebook
    public void ShareOnFacebook()
    {
        string appId = "145634995501895";
        string pictureUrl = "http://www.blikopnieuws.nl/sites/default/files/styles/nieuws-full-tn/public/artikel/logo.jpg?itok=au7xFs3Z";
        string linkUrl = "https://www.partijvoordedieren.nl/";
        string redirectUrl = "https://www.facebook.com/profile.php";
        string[] description = new string[2] { "I just completed the game Project Green Leader in the name of 'Partij voor de Dieren' with a score of: " + score.ToString("0") + "!",
            "Ik heb net het spel Project Green Leader uitgespeeld in de naam van 'Partij voor de Dieren' met een score van: " + score.ToString("0") + "!" };

        string facebookURL = "https://www.facebook.com/dialog/feed?" +
            "app_id=" + appId + "&" +
            "display=popup&" +
            "link=" + WWW.EscapeURL(linkUrl) + " & " +
            "name=" + WWW.EscapeURL("Project Green Leader") + " & " +
            "description=" + WWW.EscapeURL(description[taal]) + " & " +
            "picture=" + WWW.EscapeURL(pictureUrl);

    Application.OpenURL(facebookURL);
    }

    public void ShareOnFacebookAfterTutorial()
    {
        string appId = "145634995501895";
        string pictureUrl = "http://www.blikopnieuws.nl/sites/default/files/styles/nieuws-full-tn/public/artikel/logo.jpg?itok=au7xFs3Z";
        string linkUrl = "https://www.partijvoordedieren.nl/";
        string redirectUrl = "https://www.facebook.com/profile.php";
        string[] description = new string[2] { "I just completed the tutorial of the game Project Green Leader and I shall now make The Netherlands a green country!",
            "Ik heb net de tutorial van het spel Project Green Leader uitgespeeld en ik zal nu Nederland een groen land maken!" };

        string facebookURL = "https://www.facebook.com/dialog/feed?" +
            "app_id=" + appId + "&" +
            "display=popup&" +
            "link=" + WWW.EscapeURL(linkUrl) + " & " +
            "name=" + WWW.EscapeURL("Project Green Leader") + " & " +
            "description=" + WWW.EscapeURL(description[taal]) + " & " +
            "picture=" + WWW.EscapeURL(pictureUrl);

        Application.OpenURL(facebookURL);
    }
    #endregion

    #region LoadAndSaveXML
    //only called during development to update XML files of classes when something has been changed
    public void UpdateXMLFiles()
    {
        foreach (MapRegion region in game.regions)
        {
            foreach (RegionSector sector in region.sectors)
            {
                sector.statistics.pollution.CalculateAvgPollution();
            }
            region.statistics.UpdateSectorAvgs(region);
        }

        SaveRegions();
        SaveRegionActions();
        SaveBuildings();
        SaveGameEvents();
        SaveQuests();
        SaveCards();
    }

    public void SaveGame()
    {
        GameContainer gameContainer = new GameContainer(game);
        gameContainer.Save();
    }

    public void LoadGame()
    {
        GameContainer gameContainer = GameContainer.Load();
        game = gameContainer.game;
    }

    public void SaveRegions()
    {
        RegionContainer regionContainer = new RegionContainer(game.regions);
        regionContainer.Save();
    }

    public void LoadRegions()
    {
        RegionContainer regionContainer = RegionContainer.Load();
        game.LoadRegions(regionContainer.regions);
    }

    public void SaveGameEvents()
    {
        GameEventContainer eventContainer = new GameEventContainer(game.events);
        eventContainer.Save();
    }

    public void LoadGameEvents()
    {
        GameEventContainer eventContainer = GameEventContainer.Load();
        game.LoadGameEvents(eventContainer.events);
    }

    public void SaveRegionActions()
    {
        RegionActionContainer regionActionContainer = new RegionActionContainer(game.regions[0].actions);
        regionActionContainer.Save();
    }

    //Actions are loaded in each region seperately to avoid references
    public void LoadRegionActions()
    {
        foreach (MapRegion region in game.regions)
        {
            RegionActionContainer regionActionContainer = RegionActionContainer.Load();
            region.LoadActions(regionActionContainer.actions);
        }
    }

    public void SaveBuildings()
    {
        BuildingContainer buildingContainer = new BuildingContainer(game.regions[0].possibleBuildings);
        buildingContainer.Save();

    }

    public void LoadBuildings()
    {
        foreach (MapRegion region in game.regions)
        {
            BuildingContainer buildingContainer = BuildingContainer.Load();
            region.LoadBuildings(buildingContainer.buildings);
        }
    }

    public void SaveQuests()
    {
        QuestContainer questContainer = new QuestContainer(game.quests);
        questContainer.Save();
    }

    public void LoadQuests()
    {
        QuestContainer questContainer = QuestContainer.Load();
        game.LoadQuests(questContainer.quests);
    }

    public void SaveCards()
    {
        CardContainer cardContainer = new CardContainer(game.cards);
        cardContainer.Save();
    }

    public void LoadCards()
    {
        CardContainer cardContainer = CardContainer.Load();
        game.LoadCards(cardContainer.cards);
    }
    #endregion

    #region NewTurnUpdates
    public void NextTurn()
    {
        if (ApplicationModel.multiplayer)
        {
            game.isWaiting = false;
            game.nextTurnIsclicked = false;
            game.OtherPlayerClickedNextTurn = false;

            // Zet de Next Turn button weer op interactable en de tekst en image die bij de andere speler aangeeft dat een speler naar de volgende beurt wil weer op false
            updateUI.btnNextTurn.interactable = true;
            updateUI.txtReadyForNextTurn.gameObject.SetActive(false);
            updateUI.imgReadyForNextTurn.gameObject.SetActive(false);

            // Zet de tekst van de button weer naar volgende maand in plaats van "Waiting..."
            string[] txt = { "Volgende maand", "Next month" };
            updateUI.btnNextTurnText.text = txt[taal];

            playerController.photonView.RPC("PlayerLogChanged", PhotonTargets.Others, "Kaart van Nederland aan het bekijken", "Looking at the map of The Netherlands");
        }

        if (!game.tutorial.tutorialNextTurnDone)
            game.tutorial.tutorialNextTurnDone = true;

        UpdateRegionsPollutionInfluence();

        bool isNewYear = game.UpdateCurrentMonthAndYear();
        game.ExecuteNewMonthMethods();

        if (!ApplicationModel.multiplayer || PhotonNetwork.isMasterClient)
            UpdateEvents();

        game.gameStatistics.UpdateRegionalAvgs(game);

        UpdateQuests();
        UpdateRegionActionAvailability();

        if (isNewYear)
        {
            UpdateCards();
            IncreaseYearlyPollutionChange();
            SetYearlyTrackingData();
        }

        GenerateNewCard();
        GenerateMonthlyUpdates(isNewYear);

        //update advisors
        game.economyAdvisor.DetermineDisplayMessage(game.currentYear, game.currentMonth, game.gameStatistics.income);
        game.pollutionAdvisor.DetermineDisplayMessage(game.currentYear, game.currentMonth, game.gameStatistics.pollution);
        game.happinessAnalyst.DetermineDisplayMessage(game.currentYear, game.currentMonth, game.gameStatistics.happiness);

        UpdateTimeline();

        if (autoSave)
            EventManager.CallSaveGame();

        updateUI.setNextTurnButtonNotInteractable();

        EventManager.CallPlayNewTurnStartSFX();

        //end of game
        if (game.currentYear == 31 || game.gameStatistics.pollution == 0d)
            ShowGameScore();
    }

    #region RegionActions
    private void UpdateRegionActionAvailability()
    {
        foreach (MapRegion r in game.regions)
        {
            foreach (RegionAction ra in r.actions)
                ra.GetAvailableActions(game, r.statistics);
        }
    }
    #endregion

    #region GameEvents
    //generate new events
    private void UpdateEvents()
    {
        int activeCount = game.getActiveEventCount();
        int eventChance = 80;
        if (game.currentYear == 1 && game.currentMonth == 2)
            eventChance = 100;

        //determines with how much the eventchance will decrease after each generated event (eventchance = 0 means no event can spawn)
        int eventChanceReduction = GetEventChanceReduction();

        while (game.rnd.Next(1, 101) <= eventChance && activeCount < 4)
        {
            if (game.PossibleEventCount() > 0 && game.GetPossibleRegionsCount() > 0)
            {
                MapRegion pickedRegion = game.PickEventRegion();
                GameEvent pickedEvent = game.GetPickedEvent(pickedRegion);
                SetEventConsequences(pickedEvent);
                pickedEvent.StartEvent(game.currentYear, game.currentMonth);
                pickedRegion.AddGameEvent(pickedEvent, game.gameStatistics.happiness);
                game.AddNewEventToMonthlyReport(pickedRegion, pickedEvent);

                eventInstance = GameController.Instantiate(eventObject);
                eventInstance.GetComponent<EventObjectController>().PlaceEventIcons(this, pickedRegion, pickedEvent);

                if (ApplicationModel.multiplayer)
                {
                    if (pickedRegion.regionOwner == PhotonNetwork.player.NickName)
                        pickedEvent.isOwnEvent = true;
                    else
                        pickedEvent.isOwnEvent = false;

                    GameEvent p = pickedEvent;
                    double[] pickedConsequences0 = new double[10] { p.pickedConsequences[0].income, p.pickedConsequences[0].happiness, p.pickedConsequences[0].ecoAwareness, p.pickedConsequences[0].prosperity, p.pickedConsequences[0].pollution.airPollution, p.pickedConsequences[0].pollution.naturePollution, p.pickedConsequences[0].pollution.waterPollution, p.pickedConsequences[0].pollution.airPollutionIncrease, p.pickedConsequences[0].pollution.naturePollutionIncrease, p.pickedConsequences[0].pollution.waterPollutionIncrease };
                    double[] pickedConsequences1 = new double[10] { p.pickedConsequences[1].income, p.pickedConsequences[1].happiness, p.pickedConsequences[1].ecoAwareness, p.pickedConsequences[1].prosperity, p.pickedConsequences[1].pollution.airPollution, p.pickedConsequences[1].pollution.naturePollution, p.pickedConsequences[1].pollution.waterPollution, p.pickedConsequences[1].pollution.airPollutionIncrease, p.pickedConsequences[1].pollution.naturePollutionIncrease, p.pickedConsequences[1].pollution.waterPollutionIncrease };
                    double[] pickedConsequences2 = new double[10] { p.pickedConsequences[2].income, p.pickedConsequences[2].happiness, p.pickedConsequences[2].ecoAwareness, p.pickedConsequences[2].prosperity, p.pickedConsequences[2].pollution.airPollution, p.pickedConsequences[2].pollution.naturePollution, p.pickedConsequences[2].pollution.waterPollution, p.pickedConsequences[2].pollution.airPollutionIncrease, p.pickedConsequences[2].pollution.naturePollutionIncrease, p.pickedConsequences[2].pollution.waterPollutionIncrease };
                    double[] pickedTemporaryConsequences0 = new double[10] { p.pickedTemporaryConsequences[0].income, p.pickedTemporaryConsequences[0].happiness, p.pickedTemporaryConsequences[0].ecoAwareness, p.pickedTemporaryConsequences[0].prosperity, p.pickedTemporaryConsequences[0].pollution.airPollution, p.pickedTemporaryConsequences[0].pollution.naturePollution, p.pickedTemporaryConsequences[0].pollution.waterPollution, p.pickedTemporaryConsequences[0].pollution.airPollutionIncrease, p.pickedTemporaryConsequences[0].pollution.naturePollutionIncrease, p.pickedTemporaryConsequences[0].pollution.waterPollutionIncrease };
                    double[] pickedTemporaryConsequences1 = new double[10] { p.pickedTemporaryConsequences[1].income, p.pickedTemporaryConsequences[1].happiness, p.pickedTemporaryConsequences[1].ecoAwareness, p.pickedTemporaryConsequences[1].prosperity, p.pickedTemporaryConsequences[1].pollution.airPollution, p.pickedTemporaryConsequences[1].pollution.naturePollution, p.pickedTemporaryConsequences[1].pollution.waterPollution, p.pickedTemporaryConsequences[1].pollution.airPollutionIncrease, p.pickedTemporaryConsequences[1].pollution.naturePollutionIncrease, p.pickedTemporaryConsequences[1].pollution.waterPollutionIncrease };
                    double[] pickedTemporaryConsequences2 = new double[10] { p.pickedTemporaryConsequences[2].income, p.pickedTemporaryConsequences[2].happiness, p.pickedTemporaryConsequences[2].ecoAwareness, p.pickedTemporaryConsequences[2].prosperity, p.pickedTemporaryConsequences[2].pollution.airPollution, p.pickedTemporaryConsequences[2].pollution.naturePollution, p.pickedTemporaryConsequences[2].pollution.waterPollution, p.pickedTemporaryConsequences[2].pollution.airPollutionIncrease, p.pickedTemporaryConsequences[2].pollution.naturePollutionIncrease, p.pickedTemporaryConsequences[2].pollution.waterPollutionIncrease };

                    playerController.photonView.RPC("EventGenerated", PhotonTargets.Others, pickedRegion.name[0], pickedEvent.name,
                        pickedConsequences0, pickedConsequences1, pickedConsequences2, pickedTemporaryConsequences0,
                        pickedTemporaryConsequences1, pickedTemporaryConsequences2);
                }
            }

            eventChance -= eventChanceReduction;
        }
    }

    private int GetEventChanceReduction()
    {
        int eventChanceReduction = 100;

        if (game.currentYear >= 2)
            eventChanceReduction -= 40;
        if (game.currentYear >= 5)
            eventChanceReduction -= 20;
        if (game.currentYear >= 10)
            eventChanceReduction -= 10;
        if (game.currentYear >= 20)
            eventChanceReduction -= 10;

        return eventChanceReduction;
    }

    //randomly generates which consequences the event will have (from the options it can pick from)
    public void SetEventConsequences(GameEvent e)
    {
        e.pickedConsequences = new SectorStatistics[e.consequences.Length];
        e.pickedTemporaryConsequences = new SectorStatistics[e.consequences.Length];
        for (int i = 0; i < e.afterInvestmentConsequences.Length; i++)
        {
            e.pickedConsequences[i] = new SectorStatistics();
            e.pickedTemporaryConsequences[i] = new SectorStatistics();
            e.pickedConsequences[i].SetPickedConsequences(e.afterInvestmentConsequences[i], eventConsequenceModifiers, game.rnd);
            e.pickedTemporaryConsequences[i].SetPickedConsequences(e.afterInvestmentTemporaryConsequences[i], eventConsequenceModifiers, game.rnd);
        }
    }
    #endregion

    #region Pollution
    //increases or decreases pollution in region based on the average pollution. (pollution spreads)
    private void UpdateRegionsPollutionInfluence()
    {
        game.gameStatistics.UpdateRegionalAvgs(game);

        foreach (MapRegion region in game.regions)
        {
            double pollutionDifference = game.gameStatistics.pollution - region.statistics.avgPollution;
            double pollutionChangeValue = pollutionDifference * 0.3 / 12;
            {
                foreach (RegionSector regionSector in region.sectors)
                {
                    regionSector.statistics.pollution.ChangeAirPollution(pollutionChangeValue);
                    regionSector.statistics.pollution.ChangeNaturePollution(pollutionChangeValue);
                    regionSector.statistics.pollution.ChangeWaterPollution(pollutionChangeValue);
                }
                region.statistics.UpdateSectorAvgs(region);
            }
        }

        game.gameStatistics.UpdateRegionalAvgs(game);
    }

    //increases the "increase of pollution per year" so the game become more difficult each year.
    public void IncreaseYearlyPollutionChange()
    {
        double changeValue = 0.4 + 0.1 * game.currentYear;
        foreach (MapRegion r in game.regions)
        {
            foreach (RegionSector rs in r.sectors)
            {
                rs.statistics.pollution.ChangeAirPollutionMutation(changeValue);
                rs.statistics.pollution.ChangeNaturePollutionMutation(changeValue);
                rs.statistics.pollution.ChangeWaterPollutionMutation(changeValue);
            }
        }
    }
    #endregion

    #region ProgressReports
    private void GenerateMonthlyUpdates(bool isNewYear)
    {
        int index = 0;

        game.oldMonthlyReport = new ProgressReport(game.monthlyReport);
        GenerateMonthlyReport(index);
        index++;
        if (isNewYear)
        {
            game.oldYearlyReport = new ProgressReport(game.yearlyReport);
            GenerateYearlyReport(index);
            index++;
            game.yearlyReport.UpdateStatistics(game.regions);
        }
        else
        {
            updateUI.btnYearlyReportStats.gameObject.SetActive(false);
        }

        //GenerateCompletedEventsAndActions(index);
        index++;

        game.monthlyReport.UpdateStatistics(game.regions);
    }

    private void GenerateMonthlyReport(int index)
    {
        updateUI.btnMonthlyReportStats.gameObject.SetActive(true);
        updateUI.btnMonthlyReportStats.interactable = false;
        updateUI.InitMonthlyReport();


        Vector3 monthlyReportStartPosition = new Vector3(5, 5 + height * 2 * (2 + index), 0);
        StartCoroutine(SetMonthlyReportButtonLocation(monthlyReportStartPosition, afterActionPosition[index]));

        //updateUI.btnMonthlyReportStats.gameObject.transform.position = afterActionPosition[index];
        index++;
    }

    private void GenerateYearlyReport(int index)
    {
        updateUI.btnYearlyReportStats.gameObject.SetActive(true);
        updateUI.btnYearlyReportStats.interactable = false;
        updateUI.InitYearlyReport();

        Vector3 yearlyReportPosition = new Vector3(5, 5 + height * 2 * (2 + index), 0);
        StartCoroutine(SetYearlyReportButtonLocation(yearlyReportPosition, afterActionPosition[index]));
        //updateUI.btnYearlyReportStats.gameObject.transform.position = afterActionPosition[index];
    }
    #endregion

    #region Quests
    private void UpdateQuests()
    {
        StartNewQuests();
        CompleteActiveQuests();
    }

    private void StartNewQuests()
    {
        foreach (Quest quest in game.quests)
        {
            if (quest.startYear == game.currentYear && quest.startMonth == game.currentMonth)
            {
                quest.StartQuest();
                if (!updateUI.questsShakes)
                    StartCoroutine(updateUI.ShakeQuests());

                updateUI.imgQuestsUitroepteken.gameObject.SetActive(true);
            }
        }
    }

    private void CompleteActiveQuests()
    {
        foreach (Quest quest in game.quests)
        {
            //only check active quests
            if (quest.isActive)
            {
                //National or regional
                if (quest.questLocation == "National")
                {
                    //checks if conditions are met, (needs seperate "if" statement)
                    if (quest.NationalCompleteConditionsMet(game.gameStatistics))
                    {
                        game.gameStatistics.ModifyMoney(quest.questMoneyReward, true);
                        quest.CompleteQuest();
                        game.completedQuestsCount++;
                        if (!updateUI.questsShakes)
                            StartCoroutine(updateUI.ShakeQuests());
                    }
                }
                else
                {
                    foreach (MapRegion r in game.regions)
                    {
                        //find quest region
                        if (r.name[0] == quest.questLocation)
                        {
                            //checks if conditions are met, (needs seperate "if" statement)
                            if (quest.RegionalCompleteConditionsMet(r.statistics))
                            {
                                game.gameStatistics.ModifyMoney(quest.questMoneyReward, true);
                                quest.CompleteQuest();
                                game.completedQuestsCount++;
                                if (!updateUI.questsShakes)
                                    StartCoroutine(updateUI.ShakeQuests());
                            }
                            break;
                        }
                    }
                }
            }
        }
    }
    #endregion

    #region Cards
    public void GenerateNewCard()
    {
        if (game.currentYear == 3 && game.currentMonth == 1)
        {
            game.inventory.AddCardToInventory(new Card(game.cards[game.rnd.Next(0, game.cards.Count)]));
            game.receivedCardsCount++;
            if (!updateUI.cardsShakes)
                StartCoroutine(updateUI.ShakeCards());

            updateUI.imgCardsUitroepteken.gameObject.SetActive(true);
        }

        else if (game.rnd.Next(1, 101) <= 2 && game.currentYear >= 3)
        {
            game.inventory.AddCardToInventory(new Card(game.cards[game.rnd.Next(0, game.cards.Count)]));
            game.receivedCardsCount++;
            if (!updateUI.cardsShakes)
                StartCoroutine(updateUI.ShakeCards());

            updateUI.imgCardsUitroepteken.gameObject.SetActive(true);
        }
    }

    //yearly reward increase
    private void UpdateCards()
    {
        foreach (Card card in game.inventory.ownedCards)
        {
            if (card.currentIncrementsDone < card.maximumIncrementsDone)
                card.increaseCurrentRewards();
        }
    }
    #endregion

    #region GameScore
    public void ShowGameScore()
    {
        score = CalculateScore();
        updateUI.initEndOfGameReport(score);
        SetScoreTrackingData(score);
    }

    public double CalculateScore()
    {
        double calcScore = 0;

        calcScore += game.gameStatistics.prosperity * 100;
        calcScore += game.gameStatistics.ecoAwareness * 100;
        calcScore += game.gameStatistics.happiness * 100;
        calcScore += game.gameStatistics.income;
        calcScore += 10000 - game.gameStatistics.pollution * 100;
        calcScore += 36000 - ((game.currentYear - 1) * 12 + (game.currentMonth - 1)) * 100;

        return calcScore;
    }
    #endregion

    #region Timeline
    private void UpdateTimeline()
    {
        game.timeline.StoreTurnInTimeLine(game.gameStatistics, game.currentYear, game.currentMonth);
    }
    #endregion
    #endregion


    private void updateUIMainScreen()
    {
        // Update Text and Color values in main UI
        updateUI.updateDate(game.currentMonth, game.currentYear);
        updateUI.updateAwarness(game.gameStatistics.ecoAwareness);
        updateUI.updatePollution(game.gameStatistics.pollution);
        updateUI.updateProsperity(game.gameStatistics.prosperity);
        updateUI.updateHappiness(game.gameStatistics.happiness);

        if (!ApplicationModel.multiplayer)
            updateUI.updateMoney(game.gameStatistics.money);
        else
            updateUI.updateMoney(game.gameStatistics.playerMoney[game.gameStatistics.playerNumber]);
    }


    /* Tooltips worden niet meer getoont atm, bewaren voor als we van mening veranderen
    private void updateUITooltips()
    {
        if (updateUI.getBtnMoneyHover())
            updateUI.updateMoneyTooltip(game.gameStatistics.income);

        if (updateUI.getBtnHappinessHover())
            updateHappiness();

        if (updateUI.getBtnAwarenessHover())
            updateAwareness();

        if (updateUI.getBtnPollutionHover())
            updatePollution();

        if (updateUI.getBtnProsperityHover())
            updateProsperity();

        if (updateUI.getBtnEnergyHover())
            updateUI.updateEnergyTooltip(game.gameStatistics.energy.cleanSource,
            game.gameStatistics.energy.fossilSource, game.gameStatistics.energy.nuclearSource);
    }

    private void updateHappiness()
    {
        for (int j = 0; j < game.regions.Count; j++)
        {
            updateUI.updateHappinessTooltip(game.regions[j].statistics.happiness, j);
        }
    }

    private void updateAwareness()
    {
        for (int j = 0; j < game.regions.Count; j++)
        {
            updateUI.updateAwarnessTooltip(game.regions[j].statistics.ecoAwareness, j);
        }
    }

    private void updatePollution()
    {
        for (int j = 0; j < game.regions.Count; j++)
        {
            updateUI.updatePollutionTooltip(game.regions[j].statistics.avgPollution, j);
        }
    }

    private void updateProsperity()
    {
        for (int j = 0; j < game.regions.Count; j++)
        {
            updateUI.updateProsperityTooltip(game.regions[j].statistics.prosperity, j);
        }
    } 
    */

    // Wordt vanuit de UpdateUI class geregeld, deze code wordt niet meer gebruikt
    private void updateUIPopups()
    {
        if (updateUI.canvasOrganizationPopup.gameObject.activeSelf)
            updateUIOrganizationScreen();

        if (updateUI.canvasRegioPopup.gameObject.activeSelf)
            updateUIRegioScreen();

        if (updateUI.canvasTimelinePopup.gameObject.activeSelf)
            updateUITimelineScreen();
    }

    private void updateUIOrganizationScreen()
    {
        //int i = 0;
        //foreach (Region region in game.regions)
        //{
            // Send the income for each region, use i to determine the region
         //   updateUI.updateOrganizationScreenUI(region.statistics.income * 12, i, game.gameStatistics.money);
          //  i++;            
       // }
    }

    private void updateUIRegioScreen()
    {

    }

    private void updateUITimelineScreen()
    {

    }

    void FixedUpdate()
    {
        
    }

    public void OnRegionClick(GameObject region)
    {
        int pickedRegion = 0;
        switch (region.name)
        {
            case "Noord Nederland":
                pickedRegion = 0;
                break;
            case "Oost Nederland":
                pickedRegion = 1;
                break;
            case "West Nederland":
                pickedRegion = 2;
                break;
            case "Zuid Nederland":
                pickedRegion = 3;
                break;
        }

        MapRegion regionModel = game.regions[pickedRegion];
        updateUI.regionClick(regionModel);
    }

    void CheckEndOfGame()
    {
        if (game.currentYear == 2050)
        {
            autoEndTurn = false;
            if(game.gameStatistics.pollution < 20)
            {
                // you did it!
            }
            else
            {
                // objective failed.
            }
        }
    }

    public bool getActivePopup()
    {
        return updateUI.getPopupActive();
    }

    // Toegewezen aan de button "Maak Gebouw" vanuit de inspector
    public void btnUseBuildingPress()
    {
        MapRegion r = updateUI.regionToBeBuild;
        Building b = updateUI.buildingToBeBuild;

        // Set de building in de regio
        r.SetBuilding(b.buildingID);

        if (ApplicationModel.multiplayer)
            playerController.photonView.RPC("BuildingMade", PhotonTargets.Others, r.name[0], b.buildingID);

        // Destory de juiste building instance en verplaats hem met de nieuwe
        // Op deze manier wordt het icoontje op de map geupdate
        for (int i = 0; i < game.regions.Count; i++)
        {
            if (r == game.regions[i])
            {
                Destroy(buildingInstances[i]);

                buildingInstances[i] = GameController.Instantiate(buildingObject);
                buildingInstances[i].GetComponent<BuildingObjectController>().placeBuildingIcon(this, r, b);
            }
        }

        // Haal de kosten van het gebouw af van je money
        game.gameStatistics.ModifyMoney(b.buildingMoneyCost, false);

        // Start de popup voor building vanuit UpdateUI
        updateUI.initBuildingPopup(b, r);
    }

    // Toegewezen aan de button "Sloop Gebouw" vanuit de inspector
    public void btnDeleteBuildingPress()
    {
        MapRegion r = updateUI.buildingRegion;
        Building b = updateUI.activeBuilding;

        // Zet de building weer op null zodat er later een nieuwe gebouwd kan worden
        r.SetBuilding(null);

        if (ApplicationModel.multiplayer)
            playerController.photonView.RPC("BuildingMade", PhotonTargets.Others, r.name[0], null);

        updateUI.canvasBuildingsPopup.gameObject.SetActive(false);
        updateUI.popupActive = false;
        EventManager.CallPopupIsDisabled();

        // Destory de juiste building instance en verplaats hem met de nieuwe
        // Op deze manier wordt het icoontje op de map geupdate
        for (int i = 0; i < game.regions.Count; i++)
        {
            if (r == game.regions[i])
            {
                Destroy(buildingInstances[i]);

                buildingInstances[i] = GameController.Instantiate(buildingObject);
                buildingInstances[i].GetComponent<BuildingObjectController>().placeBuildingIcon(this, r, null);
            }
        }

        // Open de popup vanuit UpdateUI
        updateUI.initEmptyBuildingPopup(r);
    }

    // Toegewezen aan de button "Doe Event" vanuit de inspector
    public void btnDoEventClick()
    {
        // Event wordt afgerond vanuit UpdateUI
        updateUI.finishEvent();

        // Veranderd het icoontje van de event
        eventInstance = Instantiate(eventObject);
        eventInstance.GetComponent<EventObjectController>().PlaceEventIcons(this, updateUI.regionEvent, updateUI.gameEvent);
    }

    // Toegewezen aan de "Share on Facebook" button vanuit de Inspector
    public void btnShareFacebookClick()
    {
        ShareOnFacebook();
    }

    #region Multiplayer
    public void SetDelegates()
    {
        MultiplayerManager.NextTurnClicked += GetOtherPlayerNextTurn;
        MultiplayerManager.NextTurnClick += ClickedNextTurn;
        MultiplayerManager.ChangeOwnMoney += SendOwnMoneyChange;
        MultiplayerManager.ChangeOtherPlayerMoney += game.gameStatistics.ModifyMoneyOtherPlayer;
        MultiplayerManager.StartAction += StartOtherPlayerAction;
        MultiplayerManager.StartEvent += GetOtherPlayerEvent;
        MultiplayerManager.PickEventChoice += StartOtherPlayerEventChoice;
        MultiplayerManager.PlayCard += StartOtherPlayerCard;
        MultiplayerManager.Invest += GetOtherPlayerInvestment;
        MultiplayerManager.MakeBuilding += GetOtherPlayerBuilding;
        MultiplayerManager.UpdateChat += updateUI.updateChatMessages;
        MultiplayerManager.UpdateLogMessage += updateUI.SetRemotePlayerText;
        MultiplayerManager.UpdateActivityLog += updateUI.UpdateActivityLogText;
    }

    public void GetOtherPlayerNextTurn()
    {
        game.OtherPlayerClickedNextTurn = true;
        
        updateUI.txtReadyForNextTurn.gameObject.SetActive(true);
        updateUI.imgReadyForNextTurn.gameObject.SetActive(true);

        string[] txt = { PhotonNetwork.playerList[0].NickName + " is klaar voor de volgende maand", PhotonNetwork.playerList[0].NickName +  " is ready for next month" };
        updateUI.txtReadyForNextTurn.text = txt[taal];

        if (game.nextTurnIsclicked)
            EventManager.CallChangeMonth();
    }

    public void ClickedNextTurn()
    {
        game.nextTurnIsclicked = true;
        playerController.photonView.RPC("NextTurnClicked", PhotonTargets.Others);
        
        if (game.OtherPlayerClickedNextTurn)
            EventManager.CallChangeMonth();
    }

    public void SendOwnMoneyChange(double changevalue, bool isAdded)
    {
        playerController.photonView.RPC("MoneyChanged", PhotonTargets.Others, changevalue, isAdded);
    }

    public void StartOtherPlayerAction(string RegionName, string actionName, bool[] pickedSectors)
    {
        foreach (MapRegion r in game.regions)
        {
            if (r.name[0] == RegionName)
            {
                foreach (RegionAction rA in r.actions)
                {
                    if (rA.name[0] == actionName)
                    {
                        r.StartOtherPlayerAction(rA, game, pickedSectors);
                        MultiplayerManager.CallUpdateActivity(" heeft actie (" + rA.name[0] + ") gestart in " + r.name[0],
                            " started action (" + rA.name[0] + ") in " + r.name[1]);
                        return;
                    }
                }
            }
        }
    }

    //master client will generate the event, so only player 2 will execute this method
    public void GetOtherPlayerEvent(string regionName, string eventName, double[] pickedConsequences0,
        double[] pickedConsequences1, double[] pickedConsequences2, double[] pickedTemporaryConsequences0,
        double[] pickedTemporaryConsequences1, double[] pickedTemporaryConsequences2)
    {
        MapRegion pickedRegion = GetRegion(regionName);
        GameEvent pickedEvent = GetEvent(eventName);

        if (pickedRegion.regionOwner == PhotonNetwork.player.NickName)
            pickedEvent.isOwnEvent = true;
        else
            pickedEvent.isOwnEvent = false;

        pickedEvent.pickedConsequences = new SectorStatistics[pickedEvent.consequences.Length];
        pickedEvent.pickedTemporaryConsequences = new SectorStatistics[pickedEvent.consequences.Length];
        pickedEvent.pickedConsequences[0] = new SectorStatistics();
        pickedEvent.pickedConsequences[1] = new SectorStatistics();
        pickedEvent.pickedConsequences[2] = new SectorStatistics();
        pickedEvent.pickedTemporaryConsequences[0] = new SectorStatistics();
        pickedEvent.pickedTemporaryConsequences[1] = new SectorStatistics();
        pickedEvent.pickedTemporaryConsequences[2] = new SectorStatistics();

        pickedEvent.pickedConsequences[0].SetPickedConsequencesMultiplayer(pickedConsequences0);
        pickedEvent.pickedConsequences[1].SetPickedConsequencesMultiplayer(pickedConsequences1);
        pickedEvent.pickedConsequences[2].SetPickedConsequencesMultiplayer(pickedConsequences2);
        pickedEvent.pickedTemporaryConsequences[0].SetPickedConsequencesMultiplayer(pickedTemporaryConsequences0);
        pickedEvent.pickedTemporaryConsequences[1].SetPickedConsequencesMultiplayer(pickedTemporaryConsequences1);
        pickedEvent.pickedTemporaryConsequences[2].SetPickedConsequencesMultiplayer(pickedTemporaryConsequences2);

        pickedEvent.StartEvent(game.currentYear, game.currentMonth);
        pickedRegion.AddGameEvent(pickedEvent, game.gameStatistics.happiness);
        game.AddNewEventToMonthlyReport(pickedRegion, pickedEvent);

        eventInstance = GameController.Instantiate(eventObject);
        eventInstance.GetComponent<EventObjectController>().PlaceEventIcons(this, pickedRegion, pickedEvent);
    }

    public void StartOtherPlayerEventChoice(string regionName, string eventName, int pickedChoiceNumber)
    {
        MapRegion r = GetRegion(regionName);
        GameEvent e = GetEvent(eventName);
        e.SetPickedChoice(pickedChoiceNumber, game, r);
        
        /*GameObject */
        eventInstance = Instantiate(eventObject);
        eventInstance.GetComponent<EventObjectController>().PlaceEventIcons(this, r, e);

        MultiplayerManager.CallUpdateActivity(" heeft keuze [" + e.choicesDutch[pickedChoiceNumber] + "] bij event (" + e.publicEventName[0] + ") gedaan in " + r.name[0],
            " Made choice [" + e.choicesEnglish[pickedChoiceNumber] + "] at event (" + e.publicEventName[1] + ") in " + r.name[1]);
    }

    public void GetOtherPlayerInvestment(string investmentType)
    {
        string dutchInvestmentType = investmentType;
        switch (investmentType)
        {
            case "Action cost reduction":
                game.investments.InvestInActionCostReduction(game.regions);
                updateUI.setActionCostReductionInvestments();
                dutchInvestmentType = "Kosten verlagen acties";
                if (game.investments.actionCostReduction[4])
                    updateUI.btnInvestmentActionCostInvest.gameObject.SetActive(false);
                break;
            case "Better action consequences":
                game.investments.InvestInBetterActionConsequences(game.regions);
                updateUI.setActionConsequencesInvestments();
                dutchInvestmentType = "Betere consequenties acties";
                if (game.investments.betterActionConsequences[4])
                    updateUI.btnInvestmentActionConsequenceInvest.gameObject.SetActive(false);
                break;
            case "Event cost reduction":
                game.investments.InvestInGameEventCostReduction(game.events);
                updateUI.setEventCostReductionInvestments();
                dutchInvestmentType = "Kosten verlagen events";
                if (game.investments.gameEventCostReduction[4])
                    updateUI.btnInvestmentEventCostInvest.gameObject.SetActive(false);
                break;
            case "Better event consequences":
                game.investments.InvestInBetterGameEventConsequences(game.events);
                updateUI.setEventConsequencesInvestments();
                dutchInvestmentType = "Betere consequenties events";
                if (game.investments.betterGameEventConsequences[4])
                    updateUI.btnInvestmentEventConsequenceInvest.gameObject.SetActive(false);
                break;
        }
        
        MultiplayerManager.CallUpdateActivity(" heeft geïnvesteerd in: " + dutchInvestmentType,
            " has invested in: " + investmentType);
    }

    public void StartOtherPlayerCard(string regionName, double[] cardValues, bool isGlobal)
    {
        Card c = new Card();
        MapRegion r = GetRegion(regionName);
        c.SetCardReward(cardValues);

        if (isGlobal)
            c.UseCardOnCountry(game.regions, game.gameStatistics);
        else
            c.UseCardOnRegion(r, game.gameStatistics);

        MultiplayerManager.CallUpdateActivity(" heeft een kaart gebruikt op " + r.name[0],
            " used a card on " + r.name[1]);
    }

    public void GetOtherPlayerBuilding(string regionName, string buildingID)
    {
        MapRegion r = GetRegion(regionName);
        Building b = GetBuilding(buildingID, r);
        r.SetBuilding(buildingID);

        for (int i = 0; i < game.regions.Count; i++)
        {
            if (r == game.regions[i])
            {
                Destroy(buildingInstances[i]);

                buildingInstances[i] = GameController.Instantiate(buildingObject);
                buildingInstances[i].GetComponent<BuildingObjectController>().placeBuildingIcon(this, r, b);

                MultiplayerManager.CallUpdateActivity(" heeft een " + b.buildingName[0] + " gebouwd in " + r.name[0],
                    "built a " + b.buildingName[1] + " in " + r.name[1]);

                return;
            }
        }
    }

    public MapRegion GetRegion(string regionName)
    {
        foreach (MapRegion r in game.regions)
        {
            if (r.name[0] == regionName)
            {
                return r;
            }
        }

        return null;
    }

    public GameEvent GetEvent(string eventName)
    {
        foreach (GameEvent e in game.events)
        {
            if (e.name == eventName)
            {
                return e;
            }
        }

        return null;
    }

    public Building GetBuilding(string buildingID, MapRegion r)
    {
        foreach (Building b in r.possibleBuildings)
        {
            if (b.buildingID == buildingID)
            {
                return b;
            }
        }

        return null;
    }
    #endregion
}

