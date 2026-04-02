using System.Globalization;
using PlasticMes.MitsubishiSimulator.Application;
using PlasticMes.MitsubishiSimulator.Contracts;

namespace PlasticMes.MitsubishiSimulator.Adapters.Csv;

public sealed class CsvReplayScenarioLoader : IReplayScenarioLoader
{
    public async Task<ReplayScenarioLoadResult> LoadAsync(string csvPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            return ReplayScenarioLoadResult.Failure("CSV path is required.");
        }

        try
        {
            var fullPath = Path.GetFullPath(csvPath);
            if (!File.Exists(fullPath))
            {
                return ReplayScenarioLoadResult.Failure($"CSV file was not found: {fullPath}");
            }

            var lines = await File.ReadAllLinesAsync(fullPath, ct);
            if (lines.Length < 2)
            {
                return ReplayScenarioLoadResult.Failure("CSV must contain a header row and at least one data row.");
            }

            var headerFields = ParseCsvLine(lines[0]);
            var header = ParseHeader(headerFields);
            if (!header.IsSuccess)
            {
                return ReplayScenarioLoadResult.Failure(header.ErrorMessage!);
            }

            var steps = new List<ReplayStep>();
            long? firstTimestamp = null;
            long? previousTimestamp = null;

            for (var index = 1; index < lines.Length; index++)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    continue;
                }

                var rowNumber = index + 1;
                var fields = ParseCsvLine(lines[index]);
                if (fields.Count != headerFields.Count)
                {
                    return ReplayScenarioLoadResult.Failure(
                        $"Row {rowNumber} has {fields.Count} cells but the header declares {headerFields.Count} columns.");
                }

                var timeCell = fields[0].Trim();
                if (timeCell.Length == 0)
                {
                    return ReplayScenarioLoadResult.Failure($"Row {rowNumber} has an empty time cell.");
                }

                if (!long.TryParse(timeCell, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
                {
                    return ReplayScenarioLoadResult.Failure($"Row {rowNumber} contains an invalid time value '{fields[0]}'.");
                }

                if (timestamp < 0)
                {
                    return ReplayScenarioLoadResult.Failure($"Row {rowNumber} contains a negative time value.");
                }

                if (previousTimestamp is not null && timestamp < previousTimestamp.Value)
                {
                    return ReplayScenarioLoadResult.Failure($"Row {rowNumber} time is earlier than the previous row.");
                }

                firstTimestamp ??= timestamp;
                previousTimestamp = timestamp;

                var writes = new List<DeviceWrite>(header.Columns.Count);
                for (var columnIndex = 0; columnIndex < header.Columns.Count; columnIndex++)
                {
                    var rawValue = fields[columnIndex + 1].Trim();
                    if (rawValue.Length == 0)
                    {
                        return ReplayScenarioLoadResult.Failure(
                            $"Row {rowNumber} column '{header.Columns[columnIndex].HeaderName}' is empty.");
                    }

                    var write = TryParseWrite(header.Columns[columnIndex], rawValue, rowNumber, out var errorMessage);
                    if (write is null)
                    {
                        return ReplayScenarioLoadResult.Failure(errorMessage!);
                    }

                    writes.Add(write);
                }

                var offset = TimeSpan.FromMilliseconds(timestamp - firstTimestamp.Value);
                steps.Add(new ReplayStep(offset, writes, rowNumber));
            }

            if (steps.Count == 0)
            {
                return ReplayScenarioLoadResult.Failure("CSV must contain at least one non-empty data row.");
            }

            return ReplayScenarioLoadResult.Success(new RegisterReplayScenario(fullPath, header.Columns, steps));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ReplayScenarioLoadResult.Failure(ex.Message);
        }
    }

    private static HeaderParseResult ParseHeader(IReadOnlyList<string> fields)
    {
        if (fields.Count < 2)
        {
            return HeaderParseResult.Failure("CSV must declare a 'time' column and at least one register column.");
        }

        if (!string.Equals(fields[0].Trim(), "time", StringComparison.OrdinalIgnoreCase))
        {
            return HeaderParseResult.Failure("The first CSV column must be named 'time'.");
        }

        var columns = new List<ReplayRegisterColumn>(fields.Count - 1);
        var seenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < fields.Count; index++)
        {
            var headerName = fields[index].Trim();
            if (headerName.Length == 0)
            {
                return HeaderParseResult.Failure($"Header column {index + 1} is empty.");
            }

            if (!seenHeaders.Add(headerName))
            {
                return HeaderParseResult.Failure($"Header '{headerName}' is duplicated.");
            }

            if (!TryParseAddress(headerName, out var address, out var errorMessage))
            {
                return HeaderParseResult.Failure(errorMessage!);
            }

            columns.Add(new ReplayRegisterColumn(headerName, address));
        }

        return HeaderParseResult.Success(columns);
    }

    private static DeviceWrite? TryParseWrite(
        ReplayRegisterColumn column,
        string rawValue,
        int rowNumber,
        out string? errorMessage)
    {
        if (column.Address.Unit == DeviceUnit.Word)
        {
            if (!ushort.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wordValue))
            {
                errorMessage = $"Row {rowNumber} column '{column.HeaderName}' contains an invalid word value '{rawValue}'.";
                return null;
            }

            errorMessage = null;
            return new DeviceWrite(new DeviceRange(column.Address, 1), [wordValue], null);
        }

        if (!TryParseBitValue(rawValue, out var bitValue))
        {
            errorMessage = $"Row {rowNumber} column '{column.HeaderName}' contains an invalid bit value '{rawValue}'.";
            return null;
        }

        errorMessage = null;
        return new DeviceWrite(new DeviceRange(column.Address, 1), null, [bitValue]);
    }

    private static bool TryParseAddress(string headerName, out DeviceAddress address, out string? errorMessage)
    {
        address = default;
        errorMessage = null;

        if (headerName.Length < 2)
        {
            errorMessage = $"Header '{headerName}' is not a valid device address.";
            return false;
        }

        var areaCode = char.ToUpperInvariant(headerName[0]);
        var offsetText = headerName[1..].Trim();
        if (offsetText.Length == 0 ||
            !int.TryParse(offsetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset) ||
            offset < 0)
        {
            errorMessage = $"Header '{headerName}' does not contain a valid non-negative offset.";
            return false;
        }

        var parsedAddress = areaCode switch
        {
            'X' => new DeviceAddress(DeviceArea.X, offset, DeviceUnit.Bit),
            'Y' => new DeviceAddress(DeviceArea.Y, offset, DeviceUnit.Bit),
            'M' => new DeviceAddress(DeviceArea.M, offset, DeviceUnit.Bit),
            'D' => new DeviceAddress(DeviceArea.D, offset, DeviceUnit.Word),
            _ => default,
        };

        if (parsedAddress == default)
        {
            errorMessage = $"Header '{headerName}' uses an unsupported device area.";
            return false;
        }

        address = parsedAddress;
        return true;
    }

    private static bool TryParseBitValue(string rawValue, out bool bitValue)
    {
        switch (rawValue.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
                bitValue = true;
                return true;
            case "0":
            case "false":
                bitValue = false;
                return true;
            default:
                bitValue = default;
                return false;
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        if (inQuotes)
        {
            throw new InvalidOperationException("CSV line contains an unmatched quote.");
        }

        fields.Add(current.ToString());
        return fields;
    }

    private sealed record HeaderParseResult(
        bool IsSuccess,
        IReadOnlyList<ReplayRegisterColumn> Columns,
        string? ErrorMessage)
    {
        public static HeaderParseResult Success(IReadOnlyList<ReplayRegisterColumn> columns) => new(true, columns, null);

        public static HeaderParseResult Failure(string errorMessage) => new(false, [], errorMessage);
    }
}
