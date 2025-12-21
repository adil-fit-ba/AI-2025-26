/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SOFTVERSKI INTELIGENTNI AGENTI - GLAVNI PROGRAM
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Ovaj program demonstrira UNIVERZALNU arhitekturu softverskih agenata
 * kroz 6 različitih primjera:
 *
 *   A) Rule-Based Agent (Termostat)
 *   B) Supervised Learning Agent (Spam Detektor)
 *   C) Reinforcement Learning Agent (Robot)
 *   D) Human-in-the-Loop Agent (Preporuka filmova)
 *   E) LLM-Powered Agent (Korisnička podrška)
 *   F) Q-Learning Vacuum Cleaner Agent
 *
 * KLJUČNA IDEJA: Svi agenti dijele istu arhitekturu:
 *
 *      ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
 *      │  PERCEPCIJA │ ──► │   POLITIKA  │ ──► │   AKTUATOR  │
 *      │   (Sense)   │     │   (Think)   │     │    (Act)    │
 *      └─────────────┘     └─────────────┘     └─────────────┘
 *             │                   ▲                   │
 *             │                   │                   │
 *             │            ┌──────┴──────┐            │
 *             └──────────► │   UČENJE    │ ◄──────────┘
 *                          │  (Learn)    │
 *                          └─────────────┘
 */

using System;

namespace AiAgents.Demos.ConsoleApp;

public static class AiAgentsProgram
{
    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        while (true)
        {
            ShowMenu();
            var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

            switch (choice)
            {
                case "A":
                case "1":
                    ThermostatDemo.Run();
                    break;
                case "B":
                case "2":
                    SpamDetectorDemo.Run();
                    break;
                case "C":
                case "3":
                    RobotDemo.Run();
                    break;
                case "D":
                case "4":
                    MovieRecommenderDemo.Run();
                    break;
                case "E":
                case "5":
                    CustomerSupportDemo.Run();
                    break;
                case "F":
                case "6":
                    VacuumCleanerDemo.Run();
                    break;
                case "ALL":
                case "7":
                    RunAllDemos();
                    break;
                case "Q":
                case "0":
                    Console.WriteLine("\nDoviđenja!");
                    return;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[!] Nepoznata opcija. Pokušajte ponovo.");
                    Console.ResetColor();
                    break;
            }

            Console.WriteLine("\n>> Pritisni ENTER za povratak u meni...");
            Console.ReadLine();
        }
    }

    private static void ShowMenu()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("+===============================================================+");
        Console.WriteLine("|     DEMONSTRACIJA TIPOVA SOFTVERSKIH AGENATA                  |");
        Console.WriteLine("+===============================================================+");
        Console.WriteLine("|  Svi koriste ISTE INTERFEJSE, razlicite implementacije!       |");
        Console.WriteLine("+===============================================================+");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Odaberite primjer:");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  [A] PRIMJER A: Rule-Based Agent (Termostat)");
        Console.WriteLine("      - IF-THEN pravila, BEZ ucenja");
        Console.WriteLine();
        Console.WriteLine("  [B] PRIMJER B: Supervised Learning Agent (Spam Detektor)");
        Console.WriteLine("      - Uci iz labeliranih primjera, DynamicPerception");
        Console.WriteLine();
        Console.WriteLine("  [C] PRIMJER C: Reinforcement Learning Agent (Robot)");
        Console.WriteLine("      - Q-Learning, uci iz nagrada");
        Console.WriteLine();
        Console.WriteLine("  [D] PRIMJER D: Human-in-the-Loop Agent (Preporuka filmova)");
        Console.WriteLine("      - Uci iz ljudskog feedbacka, DynamicPerception");
        Console.WriteLine();
        Console.WriteLine("  [E] PRIMJER E: LLM-Powered Agent (Korisnicka podrska)");
        Console.WriteLine("      - Simulacija LLM-a, ticket queue, supervizor");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  [F] PRIMJER F: Q-Learning Vacuum Cleaner");
        Console.WriteLine("      - Kompletan RL agent sa vizualizacijom (nasljedjuje SoftwareAgent)");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [7] Pokreni SVE primjere redom (osim Vacuum Cleaner-a)");
        Console.WriteLine("  [Q] Izlaz");
        Console.ResetColor();
        Console.WriteLine();
        Console.Write("  Vas izbor: ");
    }

    private static void RunAllDemos()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("+===============================================================+");
        Console.WriteLine("|     DEMONSTRACIJA TIPOVA SOFTVERSKIH AGENATA                  |");
        Console.WriteLine("+===============================================================+");
        Console.WriteLine("|  Svi koriste ISTE INTERFEJSE, razlicite implementacije!       |");
        Console.WriteLine("+===============================================================+");
        Console.ResetColor();

        ThermostatDemo.Run();
        Console.WriteLine("\n>> Pritisni ENTER za nastavak...");
        Console.ReadLine();

        SpamDetectorDemo.Run();
        Console.WriteLine("\n>> Pritisni ENTER za nastavak...");
        Console.ReadLine();

        RobotDemo.Run();
        Console.WriteLine("\n>> Pritisni ENTER za nastavak...");
        Console.ReadLine();

        MovieRecommenderDemo.Run();
        Console.WriteLine("\n>> Pritisni ENTER za nastavak...");
        Console.ReadLine();

        CustomerSupportDemo.Run();

        Console.WriteLine("\n================================================================");
        Console.WriteLine("  ZAKLJUCAK: Sense -> Think -> Act -> Learn je UNIVERZALNA");
        Console.WriteLine("  arhitektura za SVE tipove inteligentnih agenata!");
        Console.WriteLine("================================================================");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  NAPOMENA: Vacuum Cleaner demo nije ukljucen u 'SVE' jer");
        Console.WriteLine("            zahtijeva interaktivnu vizualizaciju.");
        Console.WriteLine("            Pokrenite ga zasebno opcijom [F].");
        Console.ResetColor();
    }
}
