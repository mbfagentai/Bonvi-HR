using System.Text.Json.Nodes;
using Bonvi.Application.Blanks.Definitions;

namespace Bonvi.Application.Tests.Blanks;

/// <summary>Blanka testlarining umumiy ta'riflari.</summary>
internal static class BlankFixtures
{
    /// <summary>SPEC-blankalar §4.9 dagi HR-F06 namunasi — so'zma-so'z.</summary>
    public const string HrF06 = """
        { "schema": 1, "code": "HR-F06", "version": "1.0", "subject": "employee",
          "title": "Moslashtirish rejasi (1–30–60–90 kun) va ustoz biriktirish",
          "retention": "Nusxalar: xodim, ustoz, bo'lim rahbari, shaxsiy ish",
          "sections": [
            { "id": "hdr", "title": "Xodim", "fields": [
              { "id": "employee", "type": "ref", "refKind": "employee", "label": "Xodim F.I.Sh.", "prefill": "subject.fullName", "readOnly": true },
              { "id": "position", "type": "text", "label": "Lavozim / bo'lim", "prefill": "subject.positionName" },
              { "id": "hireDate", "type": "date", "label": "Ishga chiqqan sana", "required": true, "prefill": "subject.hireDate" },
              { "id": "probationEnd", "type": "date", "label": "Sinov muddati tugash sanasi", "prefill": "subject.probationEndDate" },
              { "id": "manager", "type": "ref", "refKind": "employee", "label": "Bevosita rahbar", "prefill": "subject.departmentHeadId" },
              { "id": "mentor", "type": "ref", "refKind": "employee", "label": "Ustoz-murabbiy", "required": true } ] },
            { "id": "s1", "title": "Ustoz-murabbiy majburiyatlari", "fields": [
              { "id": "duties", "type": "static", "text": "Kamida 1 yil shu lavozimda … birinchi 30 kunda har kuni ≥ 15 daqiqa …" } ] },
            { "id": "d7", "title": "7-kun testi", "fill": { "at": "step", "step": "day7" }, "fields": [
              { "id": "day7Percent", "type": "percent", "label": "Natija (%)", "passAt": 70, "required": true },
              { "id": "day7Pass", "type": "computed", "label": "O'tdi (≥ 70%)", "valueType": "bool",
                "expr": { "op": "gte", "args": [ { "ref": "day7Percent" }, { "const": 70 } ] } } ] },
            { "id": "d30", "title": "30-kun", "fill": { "at": "step", "step": "day30" }, "fields": [
              { "id": "day30Decision", "type": "choice", "label": "Qaror", "required": true, "options": [
                { "value": "continue", "label": "davom" }, { "value": "training", "label": "o'qitish" },
                { "value": "stop", "label": "to'xtatish", "extra": { "type": "longtext", "label": "Sabab", "required": true } } ] } ] },
            { "id": "s3", "title": "Birinchi 30 kundagi savollar", "fill": { "at": "open" }, "fields": [
              { "id": "questions", "type": "table", "minRows": 0, "maxRows": 50, "columns": [
                { "id": "date", "type": "date", "label": "Sana" },
                { "id": "question", "type": "longtext", "label": "Savol / muammo" },
                { "id": "answer", "type": "longtext", "label": "Javob / yechim (ustoz)" } ] } ] }
          ],
          "steps": [
            { "id": "signEmployee", "title": "Xodim tanishdi", "order": 1, "actor": "subjectEmployee", "mode": "either" },
            { "id": "signMentor", "title": "Ustoz", "order": 1, "actor": "field:mentor", "mode": "either" },
            { "id": "signHead", "title": "Bo'lim rahbari", "order": 1, "actor": "departmentHead", "mode": "either" },
            { "id": "signHr4", "title": "HR-4", "order": 1, "actor": "hr4", "mode": "electronic" },
            { "id": "day7", "title": "7-kun testi", "order": 2, "actor": "hr4", "mode": "electronic", "due": { "field": "hireDate", "days": 7 } },
            { "id": "day30", "title": "30-kun qarori", "order": 3, "actor": "departmentHead", "mode": "either", "due": { "field": "hireDate", "days": 30 } },
            { "id": "day90", "title": "90-kun: HR-1 qarori", "order": 6, "actor": "hr1", "mode": "electronic",
              "decision": { "required": true, "options": [ { "value": "passed", "label": "sinovdan o'tdi" }, { "value": "failed", "label": "o'tmadi" } ] },
              "due": { "field": "hireDate", "days": 90 } } ] }
        """;

    public static readonly DateOnly Today = new(2026, 11, 3);

    public const string MentorId = "0199f0a0-5e1d-7c00-8000-000000000011";

    public static BlankDefinition F06() => BlankDefinitionValidator.Load(HrF06);

    /// <summary>Bitta bo'limli ta'rif — <paramref name="fieldsJson"/> maydonlar massivi, <paramref name="stepsJson"/> qadamlar.</summary>
    public static string Single(string fieldsJson, string stepsJson = "[]")
        => $$"""{ "schema": 1, "code": "T", "version": "1", "title": "T", "subject": "employee", "sections": [ { "id": "main", "title": "M", "fields": {{fieldsJson}} } ], "steps": {{stepsJson}} }""";

    public static BlankDefinition Load(string fieldsJson, string stepsJson = "[]") => BlankDefinitionValidator.Load(Single(fieldsJson, stepsJson));

    /// <summary>F06 JSON'ini o'zgartirib qaytaradi (rad etish testlari uchun).</summary>
    public static string MutateF06(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(HrF06)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    public static JsonObject Field(JsonObject root, int section, int field)
        => root["sections"]![section]!["fields"]![field]!.AsObject();

    public static BlankAnswersOptions Options(params string[] requiredSections)
        => new(Today, requiredSections.ToHashSet(StringComparer.Ordinal));

    public static JsonObject Json(string json) => JsonNode.Parse(json)!.AsObject();
}
