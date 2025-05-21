// Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;

class StaticAnalyzerSARIF
{
    static void Main(string[] args)
    {
        Console.WriteLine("Введите полный путь к файлу для анализа:");
        string filePath = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine("Файл не найден. Проверьте путь и попробуйте снова.");
            return;
        }

        string code = File.ReadAllText(filePath);
        string cleanedCode = RemoveCommentsAndStrings(code);
        string[] lines = cleanedCode.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var results = new List<Dictionary<string, object>>();

        // Правило 1: поиск eval() (кроме C/C++)
        var evalPattern = new Regex(@"\beval\s*\(", RegexOptions.IgnoreCase);
        AddMatches(results, evalPattern, lines, filePath, "Use of eval()");

        // Правило 2: поиск пользовательских секретов (password, secret, token, apikey)
        var secretPattern = new Regex(@"(password|secret|token|apikey)[\t ]*=[\t ]*""?.+""?", RegexOptions.IgnoreCase);
        AddMatches(results, secretPattern, lines, filePath, "Possible hardcoded secret");

        // Правило 3: поиск двух одинаковых строк подряд
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == lines[i - 1].Trim() && !string.IsNullOrWhiteSpace(lines[i]))
            {
                results.Add(CreateResult(filePath, i + 1, "Duplicated consecutive line"));
            }
        }

        // Правило 4: поиск двух подряд идущих операторов присваивания с одинаковыми левыми частями
        var assignPattern = new Regex(@"^\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*=", RegexOptions.IgnoreCase);

        for (int i = 1; i < lines.Length; i++)
        {
            var m1 = assignPattern.Match(lines[i - 1]);
            var m2 = assignPattern.Match(lines[i]);
            if (m1.Success && m2.Success)
            {
                var left1 = m1.Groups[1].Value.Trim();
                var left2 = m2.Groups[1].Value.Trim();
                if (left1 == left2)
                {
                    results.Add(CreateResult(filePath, i + 1, $"Repeated assignment to variable '{left2}'"));
                }
            }
        }

        // Формируем SARIF лог
        var sarifLog = new Dictionary<string, object>
        {
            ["version"] = "2.1.0",
            ["$schema"] = "https://schemastore.azurewebsites.net/schemas/json/sarif-2.1.0-rtm.5.json",
            ["runs"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["tool"] = new Dictionary<string, object>
                    {
                        ["driver"] = new Dictionary<string, object>
                        {
                            ["name"] = "SimpleStaticAnalyzer",
                            ["version"] = "1.0.0",
                            ["rules"] = new object[] { }
                        }
                    },
                    ["results"] = results
                }
            }
        };

        string outputPath = @"C:\Users\Алекса\Desktop\resultsSAST\result.sarif";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(sarifLog, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Анализ завершён. Результаты сохранены в {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при сохранении файла: " + ex.Message);
        }
    }

    static void AddMatches(List<Dictionary<string, object>> results, Regex pattern, string[] lines, string filePath, string message)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (pattern.IsMatch(lines[i]))
            {
                results.Add(CreateResult(filePath, i + 1, message));
            }
        }
    }

    static Dictionary<string, object> CreateResult(string filePath, int line, string message)
    {
        return new Dictionary<string, object>
        {
            ["ruleId"] = message.Replace(" ", "_").Replace("'", "").Replace("(", "").Replace(")", ""),
            ["message"] = new { text = message },
            ["locations"] = new[]
            {
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new { uri = filePath },
                        region = new { startLine = line }
                    }
                }
            }
        };
    }

    static string RemoveCommentsAndStrings(string code)
    {
        // Удаляем строковые литералы (учитываем и @-строки)
        code = Regex.Replace(code, @"@?""([^""]|(""""))*""", "");
        // Удаляем однострочные комментарии //
        code = Regex.Replace(code, @"//.*", "");
        // Удаляем многострочные комментарии /* */
        code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return code;
    }
}
