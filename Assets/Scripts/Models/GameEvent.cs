﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.IO;

[Serializable]
public class GameEvent
{
    public string name { get; private set; } //id
    public string[] publicEventName { get; private set; }
    
    public string[] description { get; private set; }
    public string[] choicesDutch { get; private set; }
    public string[] choicesEnglish { get; private set; }
    public bool isUnique { get; private set; }
    public bool isGlobal { get; private set; }
    public int eventStartChance { get; private set; } //chance to actually get the event when rolled (0-100)
    public int eventIdleDuration { get; private set; } //in months
    public int eventCooldown { get; private set; } //in months
    public int[] eventDuration { get; private set; } //in months
    public int[] temporaryConsequencesDuration { get; private set; }
    public int onEventStartTemporaryConsequenceDuration { get; private set; }
    public double[] eventChoiceMoneyCost { get; private set; }
    public double[] afterInvestmentEventChoiceMoneyCost { get; private set; }
    public double[] eventChoiceMoneyReward { get; private set; }
    public int[] eventChoiceEventStartChanceModifier { get; private set; }
    public string[] possibleRegions { get; private set; }
    public int[] successChance { get; private set; } //chance to succesfully perform the choice
    public int[] increasedConsequencesModifierChance { get; private set; }
    public string[] possibleSectors { get; private set; }

    public SectorStatistics[] consequences { get; private set; }
    public SectorStatistics[] afterInvestmentConsequences { get; private set; }
    public SectorStatistics[] temporaryConsequences { get; private set; }
    public SectorStatistics[] duringEventProgressConsequences { get; private set; } //consequences after choosing an option until the event is completed
    public SectorStatistics onEventStartConsequence { get; private set; }
    public SectorStatistics onEventStartTemporaryConsequence { get; private set; }

    //choice picked events variables
    public int pickedChoiceNumber { get; private set; }
    public int pickedChoiceStartYear { get; private set; }
    public int pickedChoiceStartMonth { get; private set; }
    public int lastCompleted { get; private set; }
    public bool isIdle { get; private set; }
    public int idleTurnsLeft { get; private set; } 
    public bool isActive { get; private set; }
    public int onEventStartMonth { get; private set; }
    public int onEventStartYear { get; private set; }
    public bool isFinished { get; private set; }

    private GameEvent() { }
    
    public void StartEvent(int currentYear, int currentMonth)
    {
        onEventStartYear = currentYear;
        onEventStartMonth = currentMonth;

        isIdle = true;
        idleTurnsLeft = eventIdleDuration;
        isFinished = false;
    }

    public void SubtractIdleTurnsLeft()
    {
        idleTurnsLeft--;
    }
    
    public void FinishEvent()
    {
        pickedChoiceStartYear = 0;
        pickedChoiceStartMonth = 0;
        onEventStartYear = 0;
        onEventStartMonth = 0;
        pickedChoiceNumber = 0;

        isFinished = true;
    }

    public void CompleteEvent(Game game)
    {
        isActive = false;
        eventStartChance += eventChoiceEventStartChanceModifier[pickedChoiceNumber];
        game.gameStatistics.ModifyMoney(eventChoiceMoneyReward[pickedChoiceNumber], true);
        lastCompleted = pickedChoiceStartYear * 12 + pickedChoiceStartMonth + eventCooldown + temporaryConsequencesDuration[pickedChoiceNumber];
    }

    public void SetPickedChoice(int i, Game game, Region region)
    {
        pickedChoiceNumber = i;
        pickedChoiceStartYear = game.currentYear;
        pickedChoiceStartMonth = game.currentMonth;
        isIdle = false;
        idleTurnsLeft = 0;
        isActive = true;

        game.gameStatistics.ModifyMoney(afterInvestmentEventChoiceMoneyCost[pickedChoiceNumber], false);
        region.ImplementEventConsequences(this, duringEventProgressConsequences[pickedChoiceNumber], true, game.gameStatistics.happiness);

        if (eventDuration[pickedChoiceNumber] == 0)
        {
            game.AddCompletedEventToReports(region, this);
            region.CompleteEvent(this, game);
        }
    }

    public void SetAfterInvestmentEventChoiceMoneyCost(double modifier)
    {
        for (int i = 0; i < afterInvestmentEventChoiceMoneyCost.Length; i++)
        {
            afterInvestmentEventChoiceMoneyCost[i] -= eventChoiceMoneyCost[i] * modifier;
        }
    }

    public void SetAfterInvestmentConsequences(double modifier)
    {
        for (int i = 0; i < consequences.Length; i++)
        {
            double incomeChangeValue = consequences[i].income * modifier;
            double happinessChangeValue = consequences[i].happiness * modifier;
            double ecoAwarenessChangeValue = consequences[i].ecoAwareness * modifier;
            double prosperityChangeValue = consequences[i].prosperity * modifier;
            double airPollutionChangeValue = consequences[i].pollution.airPollution * modifier;
            double naturePollutionChangeValue = consequences[i].pollution.naturePollution * modifier;
            double waterPollutionChangeValue = consequences[i].pollution.waterPollution * modifier;
            double airPollutionIncreaseChangeValue = consequences[i].pollution.airPollutionIncrease * modifier;
            double naturePollutionIncreaseChangeValue = consequences[i].pollution.naturePollutionIncrease * modifier;
            double waterPollutionIncreaseChangeValue = consequences[i].pollution.waterPollutionIncrease * modifier;

            if (incomeChangeValue > 0)
                afterInvestmentConsequences[i].ModifyIncome(incomeChangeValue);
            else
                afterInvestmentConsequences[i].ModifyIncome(0 - incomeChangeValue);
            if (happinessChangeValue > 0)
                afterInvestmentConsequences[i].ModifyHappiness(happinessChangeValue);
            else
                afterInvestmentConsequences[i].ModifyHappiness(0 - happinessChangeValue);
            if (ecoAwarenessChangeValue > 0)
                afterInvestmentConsequences[i].ModifyEcoAwareness(ecoAwarenessChangeValue);
            else
                afterInvestmentConsequences[i].ModifyEcoAwareness(0 - ecoAwarenessChangeValue);
            if (prosperityChangeValue > 0)
                afterInvestmentConsequences[i].ModifyProsperity(prosperityChangeValue);
            else
                afterInvestmentConsequences[i].ModifyProsperity(0 - prosperityChangeValue);
            if (airPollutionChangeValue < 0)
                afterInvestmentConsequences[i].pollution.ChangeAirPollution(airPollutionChangeValue);
            else
                afterInvestmentConsequences[i].pollution.ChangeAirPollution(0 - airPollutionChangeValue);
            if (naturePollutionChangeValue < 0)
                afterInvestmentConsequences[i].pollution.ChangeNaturePollution(naturePollutionChangeValue);
            else
                afterInvestmentConsequences[i].pollution.ChangeNaturePollution(0 - naturePollutionChangeValue);
            if (waterPollutionChangeValue < 0)
                afterInvestmentConsequences[i].pollution.ChangeWaterPollution(waterPollutionChangeValue);
            else
                afterInvestmentConsequences[i].pollution.ChangeWaterPollution(0 - waterPollutionChangeValue);
            if (airPollutionIncreaseChangeValue < 0)
                afterInvestmentConsequences[i].pollution.ChangeAirPollutionMutation(airPollutionIncreaseChangeValue);
            else
                afterInvestmentConsequences[i].pollution.ChangeAirPollutionMutation(0 - airPollutionIncreaseChangeValue);
            if (naturePollutionIncreaseChangeValue < 0)
                afterInvestmentConsequences[i].pollution.ChangeNaturePollutionMutation(naturePollutionIncreaseChangeValue);
            else
                afterInvestmentConsequences[i].pollution.ChangeNaturePollutionMutation(0 - naturePollutionIncreaseChangeValue);
            if (waterPollutionIncreaseChangeValue < 0)
                afterInvestmentConsequences[i].pollution.ChangeWaterPollutionMutation(waterPollutionIncreaseChangeValue);
            else
                afterInvestmentConsequences[i].pollution.ChangeWaterPollutionMutation(0 - waterPollutionIncreaseChangeValue);
        }
    }
}

