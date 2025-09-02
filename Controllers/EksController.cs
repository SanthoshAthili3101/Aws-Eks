using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Aws_Eks.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EksController : ControllerBase
    {
        // GET: api/<EksController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<EksController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<EksController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<EksController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<EksController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
