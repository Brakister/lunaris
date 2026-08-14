using System.Globalization;

namespace Lunaris.Search.CalculatorProvider;

/// <summary>
/// Tiny recursive-descent expression evaluator. Supports + - * / % ^, parentheses,
/// unary minus, trailing percent and common functions. Never uses eval or DataTable.
/// </summary>
public static class MathExpressionParser
{
    private sealed class Reader
    {
        public string Text = string.Empty;
        public int Pos;
        public bool Ok = true;
        public string Error = string.Empty;
    }

    public static bool TryEvaluate(string expression, out double value, out string error)
    {
        value = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var r = new Reader { Text = expression };
        var result = ParseExpression(r);
        SkipWhitespace(r);

        if (!r.Ok)
        {
            error = r.Error;
            return false;
        }

        if (r.Pos != r.Text.Length)
        {
            error = "Expressão incompleta";
            return false;
        }

        if (!double.IsFinite(result))
        {
            error = "Resultado inválido";
            return false;
        }

        value = result;
        return true;
    }

    private static double ParseExpression(Reader r)
    {
        var value = ParseTerm(r);
        while (true)
        {
            SkipWhitespace(r);
            if (Peek(r) is '+' or '-')
            {
                var op = Next(r);
                var rhs = ParseTerm(r);
                value = op == '+' ? value + rhs : value - rhs;
            }
            else
            {
                return value;
            }
        }
    }

    private static double ParseTerm(Reader r)
    {
        var value = ParseFactor(r);
        while (true)
        {
            SkipWhitespace(r);
            var c = Peek(r);
            if (c is '*' or '/' or '%')
            {
                var op = Next(r);
                var rhs = ParseFactor(r);
                value = op switch
                {
                    '*' => value * rhs,
                    '/' when rhs == 0 => Fail(r, "Divisão por zero"),
                    '/' => value / rhs,
                    '%' when rhs == 0 => Fail(r, "Divisão por zero"),
                    '%' => value % rhs,
                    _ => value,
                };
            }
            else
            {
                return value;
            }
        }
    }

    private static double ParseFactor(Reader r)
    {
        SkipWhitespace(r);
        var c = Peek(r);

        if (c == '-')
        {
            Next(r);
            return -ParseFactor(r);
        }

        if (c == '+')
        {
            Next(r);
            return ParseFactor(r);
        }

        var value = ParsePower(r);

        // Trailing percent: 25% -> 0.25
        SkipWhitespace(r);
        if (Peek(r) == '%')
        {
            Next(r);
            return value / 100.0;
        }

        return value;
    }

    private static double ParsePower(Reader r)
    {
        SkipWhitespace(r);
        var baseValue = ParsePrimary(r);

        SkipWhitespace(r);
        if (Peek(r) == '^')
        {
            Next(r);
            var exponent = ParseFactor(r);
            return Math.Pow(baseValue, exponent);
        }

        return baseValue;
    }

    private static double ParsePrimary(Reader r)
    {
        SkipWhitespace(r);
        var c = Peek(r);

        if (char.IsDigit(c) || c == '.')
            return ParseNumber(r);

        if (char.IsLetter(c))
            return ParseIdentifierOrFunction(r);

        if (c == '(')
        {
            Next(r);
            var value = ParseExpression(r);
            SkipWhitespace(r);
            if (Peek(r) != ')')
                return Fail(r, "Parêntese não fechado");
            Next(r);
            return value;
        }

        return Fail(r, "Expressão inválida");
    }

    private static double ParseIdentifierOrFunction(Reader r)
    {
        var start = r.Pos;
        while (r.Pos < r.Text.Length && char.IsLetter(r.Text[r.Pos]))
            r.Pos++;

        var identifier = r.Text[start..r.Pos];
        SkipWhitespace(r);

        if (Peek(r) == '(')
        {
            Next(r);
            var args = new List<double>();
            while (true)
            {
                SkipWhitespace(r);
                if (Peek(r) == ')')
                {
                    Next(r);
                    break;
                }
                args.Add(ParseExpression(r));
                SkipWhitespace(r);
                if (Peek(r) == ',')
                    Next(r);
                else if (Peek(r) == ')')
                {
                    Next(r);
                    break;
                }
                else
                    return Fail(r, $"Erro na função {identifier}");
            }

            return ApplyFunction(r, identifier.ToLowerInvariant(), args);
        }

        return identifier.ToLowerInvariant() switch
        {
            "pi" => Math.PI,
            "e" => Math.E,
            _ => Fail(r, $"Valor desconhecido: {identifier}"),
        };
    }

    private static double ApplyFunction(Reader r, string name, List<double> args)
    {
        double one() => RequireArg(r, name, args, 1, 0);

        switch (name)
        {
            case "sqrt": return Math.Sqrt(one());
            case "cbrt": return Math.Cbrt(one());
            case "sin": return Math.Sin(one());
            case "cos": return Math.Cos(one());
            case "tan": return Math.Tan(one());
            case "asin": return Math.Asin(one());
            case "acos": return Math.Acos(one());
            case "atan": return Math.Atan(one());
            case "abs": return Math.Abs(one());
            case "floor": return Math.Floor(one());
            case "ceil": return Math.Ceiling(one());
            case "round": return Math.Round(one());
            case "exp": return Math.Exp(one());
            case "ln": return Math.Log(one());
            case "log": return Math.Log10(one());
            case "sign": return Math.Sign(one());
            case "min": return RequireArg(r, name, args, 2, 0) < RequireArg(r, name, args, 2, 1) ? args[0] : args[1];
            case "max": return args[0] > args[1] ? args[0] : args[1];
            case "pow": return Math.Pow(RequireArg(r, name, args, 2, 0), RequireArg(r, name, args, 2, 1));
            default: return Fail(r, $"Função desconhecida: {name}");
        }
    }

    private static double RequireArg(Reader r, string name, List<double> args, int count, int index)
    {
        if (args.Count < count)
            return Fail(r, $"Função {name} requer {count} argumento(s)");
        return args[index];
    }

    private static double ParseNumber(Reader r)
    {
        var start = r.Pos;
        var hasDot = false;
        while (r.Pos < r.Text.Length)
        {
            var c = r.Text[r.Pos];
            if (char.IsDigit(c))
            {
                r.Pos++;
            }
            else if (c == '.' && !hasDot)
            {
                hasDot = true;
                r.Pos++;
            }
            else
            {
                break;
            }
        }

        var token = r.Text[start..r.Pos];
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return Fail(r, "Número inválido");
    }

    private static char Peek(Reader r) => r.Pos < r.Text.Length ? r.Text[r.Pos] : '\0';

    private static char Next(Reader r) => r.Pos < r.Text.Length ? r.Text[r.Pos++] : '\0';

    private static void SkipWhitespace(Reader r)
    {
        while (r.Pos < r.Text.Length && char.IsWhiteSpace(r.Text[r.Pos]))
            r.Pos++;
    }

    private static double Fail(Reader r, string message)
    {
        r.Ok = false;
        r.Error = message;
        return 0;
    }
}