/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT CONSOLE - UI HELPERS
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using AiAgents.SpamAgent.Domain;

namespace AiAgents.SpamAgent.Console.CLI;

public static class ConsoleUI
{
    public static void WriteHeader(string title)
    {
        System.Console.WriteLine();
        System.Console.ForegroundColor = ConsoleColor.Cyan;
        System.Console.WriteLine($"╔══════════════════════════════════════════════════════════════╗");
        System.Console.WriteLine($"║  {title,-60}║");
        System.Console.WriteLine($"╚══════════════════════════════════════════════════════════════╝");
        System.Console.ResetColor();
    }

    public static void WriteSubHeader(string title)
    {
        System.Console.WriteLine();
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine($"── {title} ──");
        System.Console.ResetColor();
    }

    public static void WriteSuccess(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine($"[OK] {message}");
        System.Console.ResetColor();
    }

    public static void WriteError(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.WriteLine($"[ERROR] {message}");
        System.Console.ResetColor();
    }

    public static void WriteWarning(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine($"[WARN] {message}");
        System.Console.ResetColor();
    }

    public static void WriteInfo(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.White;
        System.Console.WriteLine($"[INFO] {message}");
        System.Console.ResetColor();
    }

    public static void WriteDecision(SpamDecision decision, double pSpam)
    {
        var color = decision switch
        {
            SpamDecision.Allow => ConsoleColor.Green,
            SpamDecision.Block => ConsoleColor.Red,
            SpamDecision.PendingReview => ConsoleColor.Yellow,
            _ => ConsoleColor.White
        };

        System.Console.ForegroundColor = color;
        System.Console.Write($"  pSpam={pSpam:F3} → {decision}");
        System.Console.ResetColor();
    }

    public static void WriteMessagePreview(string text, int maxLength = 50)
    {
        var preview = text.Length > maxLength 
            ? text.Substring(0, maxLength) + "..." 
            : text;
        
        System.Console.ForegroundColor = ConsoleColor.DarkGray;
        System.Console.Write($"  \"{preview}\"");
        System.Console.ResetColor();
    }

    public static void WriteMetrics(double accuracy, double precision, double recall, double f1)
    {
        System.Console.WriteLine($"  Accuracy:  {accuracy:P2}");
        System.Console.WriteLine($"  Precision: {precision:P2}");
        System.Console.WriteLine($"  Recall:    {recall:P2}");
        System.Console.WriteLine($"  F1 Score:  {f1:P2}");
    }

    public static void WriteTable(string[] headers, string[][] rows)
    {
        // Izračunaj širine kolona
        var widths = new int[headers.Length];
        for (int i = 0; i < headers.Length; i++)
        {
            widths[i] = headers[i].Length;
            foreach (var row in rows)
            {
                if (i < row.Length && row[i].Length > widths[i])
                    widths[i] = row[i].Length;
            }
        }

        // Header
        System.Console.ForegroundColor = ConsoleColor.Cyan;
        for (int i = 0; i < headers.Length; i++)
        {
            System.Console.Write($"  {headers[i].PadRight(widths[i])}");
        }
        System.Console.WriteLine();
        System.Console.ResetColor();

        // Separator
        System.Console.ForegroundColor = ConsoleColor.DarkGray;
        for (int i = 0; i < headers.Length; i++)
        {
            System.Console.Write($"  {new string('-', widths[i])}");
        }
        System.Console.WriteLine();
        System.Console.ResetColor();

        // Rows
        foreach (var row in rows)
        {
            for (int i = 0; i < headers.Length && i < row.Length; i++)
            {
                System.Console.Write($"  {row[i].PadRight(widths[i])}");
            }
            System.Console.WriteLine();
        }
    }

    public static void WriteHelp()
    {
        WriteHeader("SPAM AGENT - KOMANDE");
        
        System.Console.WriteLine(@"
  OSNOVNE KOMANDE:
    status                    Prikaži status sistema
    help                      Prikaži ovu pomoć
    exit                      Izađi iz aplikacije

  DATASET & IMPORT:
    import [--force]          Importuj UCI dataset u bazu

  TRENING:
    train <light|medium|full> [--activate]   Treniraj novi model
    activate <version>        Aktiviraj postojeći model
    models                    Prikaži sve verzije modela

  PROCESIRANJE:
    enqueue <count>           Dodaj poruke u queue iz validation seta
    run <steps>               Procesiraj N poruka iz queue-a
    add <text>                Dodaj novu poruku u queue

  REVIEW:
    review [--take N]         Pregled i označavanje pending poruka

  POSTAVKE:
    set thresholds <allow> <block>   Postavi pragove odlučivanja
    set retrain-threshold <N>        Postavi prag za auto-retrain
    toggle auto-retrain <on|off>     Uključi/isključi auto-retrain
");
    }
}
