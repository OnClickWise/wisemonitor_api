using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using System.Threading.Tasks;


namespace WiseMonitor.Api.Services
{
    public interface IOrganizationService
    {
        Task<ServiceResult<RegisterOrganizationResultDTO>> RegisterOrganizationAsync(RegisterOrganizationDTO dto);
    }
}

