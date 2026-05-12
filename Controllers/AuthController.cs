using Microsoft.AspNetCore.Mvc;

namespace Palloncino.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController() : ControllerBase
{
    [HttpPost("register")]
    public Task<ActionResult> RegisterNewUser()
    {
        try
        {
            throw new NotImplementedException();
        }
        catch (System.Exception)
        {
            
            throw;
        }
    }

    [HttpPost("login")]
    public Task<ActionResult> LoginUser()
    {
        try
        {
            throw new NotImplementedException();
        }
        catch (System.Exception)
        {
            
            throw;
        }
    }


}