
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Enums;
using System.Globalization;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Seed
{
    public static class AdministrativeSeed
    {
        private const string SeedFileName = "vn_administrative_units_seed.csv";

        public static AdministrativeProvince[] GetProvinces()
        {
            return ReadSeedRows()
                .Where(x => x.Entity == AdministrativeSeedEntity.Province)
                .Select(x => new AdministrativeProvince
                {
                    Code = x.Code,
                    Name = x.Name,
                    Type = Enum.Parse<ProvinceType>(x.Type),
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToArray();
        }

        public static AdministrativeWard[] GetWards()
        {
            return ReadSeedRows()
                .Where(x => x.Entity == AdministrativeSeedEntity.Ward)
                .Select(x => new AdministrativeWard
                {
                    Code = x.Code,
                    ProvinceCode = x.ProvinceCode,
                    Name = x.Name,
                    Type = Enum.Parse<WardType>(x.Type),
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToArray();
        }

        private static List<AdministrativeSeedRow> ReadSeedRows()
        {
            var seedPath = ResolveSeedFilePath();
            var lines = File.ReadAllLines(seedPath, Encoding.UTF8);

            if (lines.Length <= 1)
            {
                return [];
            }

            var rows = new List<AdministrativeSeedRow>(lines.Length - 1);

            foreach (var line in lines.Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var values = ParseCsvLine(line);

                if (values.Count != 8)
                {
                    throw new InvalidOperationException(
                        $"Dòng dữ liệu hành chính không đúng định dạng trong {seedPath}: {line}");
                }

                rows.Add(new AdministrativeSeedRow
                {
                    Entity = Enum.Parse<AdministrativeSeedEntity>(values[0]),
                    Code = values[1],
                    ProvinceCode = values[2],
                    Name = values[3],
                    Type = values[4],
                    IsActive = bool.Parse(values[5]),
                    CreatedAt = DateTimeOffset.Parse(values[6], CultureInfo.InvariantCulture),
                    UpdatedAt = DateTimeOffset.Parse(values[7], CultureInfo.InvariantCulture)
                });
            }

            return rows;
        }

        private static string ResolveSeedFilePath()
        {
            var startDirectories = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };

            foreach (var startDirectory in startDirectories)
            {
                var directory = new DirectoryInfo(startDirectory);

                while (directory is not null)
                {
                    var candidates = new[]
                    {
                        Path.Combine(directory.FullName, "server", "data", SeedFileName),
                        Path.Combine(directory.FullName, "data", SeedFileName)
                    };

                    foreach (var candidate in candidates)
                    {
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }

                    directory = directory.Parent;
                }
            }

            throw new FileNotFoundException(
                $"Không tìm thấy file seed dữ liệu hành chính '{SeedFileName}'. File cần nằm trong 'server/data' hoặc 'data'.");
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var character = line[i];

                if (character == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (character == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(character);
            }

            values.Add(current.ToString());
            return values;
        }

        private enum AdministrativeSeedEntity
        {
            Province,
            Ward
        }

        private sealed class AdministrativeSeedRow
        {
            public AdministrativeSeedEntity Entity { get; set; }

            public string Code { get; set; } = string.Empty;

            public string ProvinceCode { get; set; } = string.Empty;

            public string Name { get; set; } = string.Empty;

            public string Type { get; set; } = string.Empty;

            public bool IsActive { get; set; }

            public DateTimeOffset CreatedAt { get; set; }

            public DateTimeOffset UpdatedAt { get; set; }
        }
    }
}
