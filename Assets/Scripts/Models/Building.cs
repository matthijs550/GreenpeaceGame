﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


//this class stores the values of the buildings in a region
[Serializable]
public class Building
{
    public string buildingID { get; private set; }
    public string[] buildingName { get; private set; }
    public double incomeModifier { get; private set; }
    public double pollutionModifier { get; private set; }
    public double happinessModifier { get; private set; }
    public double buildingMoneyCost { get; private set; }

    public Building() { }
}

