using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Services.Implementations;


namespace Palloncino.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BranchController(
    IMapper mapper,
    BranchService branchService
) : ControllerBase
{
    [Authorize(Roles = "Admin")]
    [HttpPost("branch")]
    public async Task<ActionResult> CreateBranch([FromBody] CreateBranchDto dto)
    {
        try
        {
            var newBranch = mapper.Map<Branch>(dto);
            var res = await branchService.CreateBranchAsync(newBranch);
            return Ok(res);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [Authorize(Roles = "Admin")]
    [HttpGet("{id}")]
    public async Task<ActionResult> GetBranchById(int id)
    {
        try
        {
            var branch = await branchService.GetBranchByIdAsync(id);
            return Ok(branch);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult> GetAllBranches()
    {
        try
        {
            var branches = await branchService.GetAllBranchesAsync();
            return Ok(branches);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateBranch(int id, UpdateBranchDto dto)
    {
        try
        {
            var branch = mapper.Map<Branch>(dto);
            branch.Id = id;
            var newBranch = await branchService.UpdateBranchAsync(branch) 
            ?? throw new Exception("Branch not updated");
            return Ok(newBranch);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteBranch(int id)
    {
        try
        {
            var IsDeleted = await branchService.SoftDeleteBranchAsync(id,GetCurrentUserId());
            if(!IsDeleted)
                throw new Exception("branch not deleted");
            return Ok("Branch deleted");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    
    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirst(u=>u.Type == "userId")?.Value ?? "0");
    }
}