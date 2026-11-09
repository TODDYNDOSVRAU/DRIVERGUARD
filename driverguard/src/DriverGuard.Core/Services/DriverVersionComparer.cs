namespace DriverGuard.Core.Services;

/// <summary>
/// Implementa as regras oficiais do subsistema Plug and Play do Windows para ranqueamento e comparação de drivers.
/// Regra 1: A data do driver (DriverDate) tem precedência primária.
/// Regra 2: Se as datas forem idênticas (ou uma for ausente), a versão numérica quad-part (Major.Minor.Build.Revision) desempata.
/// </summary>
public static class DriverVersionComparer
{
    /// <summary>
    /// Compara se o candidato é estritamente mais novo que o driver atual instalado.
    /// Retorna true apenas se a data for posterior, ou se a data for idêntica e a versão for estritamente superior.
    /// </summary>
    public static bool IsCandidateNewer(DateTime? currentDate, string currentVersion, DateTime? candidateDate, string candidateVersion)
    {
        // 1. Comparação de datas quando ambas estiverem disponíveis
        if (currentDate.HasValue && candidateDate.HasValue)
        {
            // Trunca para data sem horário
            var currD = currentDate.Value.Date;
            var candD = candidateDate.Value.Date;

            if (candD > currD)
                return true;

            if (candD < currD)
                return false;

            // Se as datas forem exatamente iguais, prossegue para desempate por número de versão
        }
        else if (!currentDate.HasValue && candidateDate.HasValue)
        {
            // Dispositivo atual sem data; se o candidato tem data válida e versão >= atual, aceita
            if (CompareVersionStrings(candidateVersion, currentVersion) >= 0)
                return true;
        }

        // 2. Desempate ou comparação por versão numérica
        return CompareVersionStrings(candidateVersion, currentVersion) > 0;
    }

    /// <summary>
    /// Compara duas strings de versão no padrão 4-part do Windows (ex: 31.0.15.5186 vs 30.0.14.7111).
    /// Retorna > 0 se v1 > v2, 0 se iguais, < 0 se v1 < v2.
    /// </summary>
    public static int CompareVersionStrings(string? version1, string? version2)
    {
        if (string.IsNullOrWhiteSpace(version1) && string.IsNullOrWhiteSpace(version2))
            return 0;
        if (string.IsNullOrWhiteSpace(version1))
            return -1;
        if (string.IsNullOrWhiteSpace(version2))
            return 1;

        if (Version.TryParse(CleanVersion(version1), out var parsed1) &&
            Version.TryParse(CleanVersion(version2), out var parsed2))
        {
            return parsed1.CompareTo(parsed2);
        }

        // Fallback para segmentação manual numérica caso haja formatos especiais
        var parts1 = SplitVersion(version1);
        var parts2 = SplitVersion(version2);

        int maxLen = Math.Max(parts1.Length, parts2.Length);
        for (int i = 0; i < maxLen; i++)
        {
            long num1 = i < parts1.Length ? parts1[i] : 0;
            long num2 = i < parts2.Length ? parts2[i] : 0;

            if (num1 != num2)
                return num1.CompareTo(num2);
        }

        return 0;
    }

    private static string CleanVersion(string v)
    {
        // Remove caracteres não numéricos exceto pontos
        var chars = v.Where(c => char.IsDigit(c) || c == '.').ToArray();
        string clean = new string(chars).Trim('.');

        // Garante pelo menos 2 partes para Version.TryParse
        var dotCount = clean.Count(c => c == '.');
        if (dotCount == 0 && clean.Length > 0)
            return $"{clean}.0";

        return clean;
    }

    private static long[] SplitVersion(string v)
    {
        return v.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p =>
                {
                    var digits = new string(p.TakeWhile(char.IsDigit).ToArray());
                    return long.TryParse(digits, out var val) ? val : 0;
                })
                .ToArray();
    }
}
