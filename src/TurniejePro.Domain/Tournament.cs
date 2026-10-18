using System;
using System.Collections.Generic;
using System.Text;

namespace TurniejePro.Domain;

public class Tournament
{
    public string Name { get; set; }
    public bool IsStarted { get; private set; }

    public Tournament(string name)
    {
        Name = name;
        IsStarted = false; // Nowy turniej domyślnie nierozpoczęty
    }
}
