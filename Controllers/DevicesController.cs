using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Services;
using System.Security.Claims;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Garante que apenas usuários autenticados podem acessar
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceService _deviceService;

        public DevicesController(IDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        // Helper para pegar OrgId do token/claims
        private Guid GetOrgId()
        {
            var orgIdClaim = User.FindFirst("orgId")?.Value;
            return orgIdClaim != null ? Guid.Parse(orgIdClaim) : Guid.Empty;
        }

        // POST api/devices
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DeviceCreateDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var orgId = GetOrgId();
            if (orgId == Guid.Empty)
                return Forbid("Organização inválida.");

            var device = new Device
            {
                Id = Guid.NewGuid(),
                Hostname = dto.Hostname,
                IpAddress = dto.IpAddress,
                OrganizationId = orgId
            };

            var created = await _deviceService.CreateDeviceAsync(device, orgId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // GET api/devices
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var orgId = GetOrgId();
            var devices = await _deviceService.GetAllDevicesAsync(orgId);
            return Ok(devices);
        }

        // GET api/devices/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var orgId = GetOrgId();
            var device = await _deviceService.GetDeviceByIdAsync(id, orgId);

            if (device == null)
                return NotFound();

            return Ok(device);
        }

        // PUT api/devices/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DeviceUpdateDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var orgId = GetOrgId();
            var existing = await _deviceService.GetDeviceByIdAsync(id, orgId);
            if (existing == null)
                return NotFound();

    
            existing.Hostname = dto.Hostname;
            existing.IpAddress = dto.IpAddress;

            var updated = await _deviceService.UpdateDeviceAsync(existing, orgId);
            return Ok(updated);
        }

        // DELETE api/devices/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var orgId = GetOrgId();
            var success = await _deviceService.DeleteDeviceAsync(id, orgId);

            if (!success)
                return NotFound();

            return NoContent();
        }
    }
}
