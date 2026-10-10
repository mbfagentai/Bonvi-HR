using System.Net;
using Bonvi.IsupGateway.Tunnel;

namespace Bonvi.IsupGateway.Tests;

/// <summary>ISAPI javob tanasi → HTTP holati (SPEC-isup §C).</summary>
public sealed class IsapiStatusMapperTests
{
    [Theory]
    [InlineData("{\"statusCode\":1,\"statusString\":\"OK\"}", HttpStatusCode.OK)]
    [InlineData("{\"statusCode\":7,\"statusString\":\"Reboot Required\"}", HttpStatusCode.OK)]
    [InlineData("{\"statusCode\":2,\"statusString\":\"Device Busy\"}", HttpStatusCode.ServiceUnavailable)]
    [InlineData("{\"statusCode\":3,\"statusString\":\"Device Error\"}", HttpStatusCode.InternalServerError)]
    [InlineData("{\"statusCode\":4,\"statusString\":\"Invalid Operation\",\"subStatusCode\":\"notSupport\"}", HttpStatusCode.BadRequest)]
    [InlineData("{\"statusCode\":5,\"statusString\":\"Invalid Format\"}", HttpStatusCode.BadRequest)]
    [InlineData("{\"statusCode\":6,\"statusString\":\"Invalid Content\",\"subStatusCode\":\"employeeNoAlreadyExist\"}", HttpStatusCode.BadRequest)]
    [InlineData("{\"statusCode\":99}", HttpStatusCode.InternalServerError)]
    public void JSON_ResponseStatus(string body, HttpStatusCode expected)
        => IsapiStatusMapper.Map(body).ShouldBe(expected);

    [Theory]
    [InlineData(1, HttpStatusCode.OK)]
    [InlineData(4, HttpStatusCode.BadRequest)]
    [InlineData(6, HttpStatusCode.BadRequest)]
    [InlineData(2, HttpStatusCode.ServiceUnavailable)]
    public void XML_ResponseStatus(int code, HttpStatusCode expected)
    {
        var body =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<ResponseStatus version=\"1.0\" xmlns=\"http://www.hikvision.com/ver10/XMLSchema\">" +
            "<requestURL></requestURL>" +
            $"<statusCode>{code}</statusCode><statusString>x</statusString></ResponseStatus>";

        IsapiStatusMapper.Map(body).ShouldBe(expected);
    }

    [Fact]
    public void Qurilma_malumoti_XML_200()
        => IsapiStatusMapper.Map(
                "<?xml version=\"1.0\"?><DeviceInfo><deviceName>Access Controller</deviceName><serialNumber>X</serialNumber></DeviceInfo>")
            .ShouldBe(HttpStatusCode.OK);

    [Fact]
    public void Hodisalar_sahifasi_200()
        => IsapiStatusMapper.Map(
                "{\"AcsEvent\":{\"searchID\":\"a\",\"responseStatusStrg\":\"OK\",\"numOfMatches\":0,\"totalMatches\":0}}")
            .ShouldBe(HttpStatusCode.OK);

    [Fact]
    public void Ichki_statusCode_xato_deb_oqilmaydi()
        => IsapiStatusMapper.Map("{\"UserInfoSearch\":{\"UserInfo\":[{\"statusCode\":4}]}}")
            .ShouldBe(HttpStatusCode.OK);

    [Fact]
    public void Yuz_soni_javobi_statusCode_1_va_malumot_200()
        => IsapiStatusMapper.Map(
                "{\"statusCode\":1,\"statusString\":\"OK\",\"subStatusCode\":\"ok\",\"FDRecordDataInfo\":[{\"FDID\":\"1\",\"recordDataNumber\":434}]}")
            .ShouldBe(HttpStatusCode.OK);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("matn")]
    [InlineData("{buzilgan")]
    [InlineData("<buzilgan")]
    public void Tushunarsiz_tana_200(string body)
        => IsapiStatusMapper.Map(body).ShouldBe(HttpStatusCode.OK);
}
