using NUnit.Framework;

namespace UnityGameJam.Test
{
    /// <summary>
    /// 테스트 어셈블리가 Runtime·Editor 어셈블리와 함께 컴파일·실행되는지 확인한다.
    /// </summary>
    public class AssemblySmokeTest
    {
        [Test]
        public void 테스트_어셈블리가_실행된다()
        {
            Assert.Pass();
        }

        [Test]
        public void Newtonsoft_Json을_참조할_수_있다()
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(new { Value = 1 });

            Assert.AreEqual("{\"Value\":1}", json);
        }
    }
}
