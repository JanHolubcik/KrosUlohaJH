﻿using KrosUlohaJH.Helpers;
using KrosUlohaJH.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KrosUlohaJH.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FirmaController : ControllerBase
    {
        private readonly StrukturaFirmyContext _context;

        public FirmaController(StrukturaFirmyContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<Firma>> PostOrUpdateFirma(FirmaDto FirmaDTO)
        {

            var mapper = MapperConfig.InitializeAutomapper();
            var Firma = mapper.Map<Firma>(FirmaDTO);

            var (success, result) = await CreateOrUpdate(Firma);
            return result;
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> PostBulkFirma([FromBody] List<FirmaDto> Firma)
        {
            var errors = new List<object>();
            var success = new List<Firma>();
            var mapper = MapperConfig.InitializeAutomapper();

            foreach (var FirmaDTO in Firma)
            {
                var z = mapper.Map<Firma>(FirmaDTO);
                var (ok, result) = await CreateOrUpdate(z);

                if (ok && result is ObjectResult r1 && r1.Value is Firma zam)
                    success.Add(zam);
                else
                    errors.Add(new { kod = z.Kod, chyba = (result as ObjectResult)?.Value });
            }

            return Ok(new
            {
                uspesne = success.Count,
                neuspesne = errors.Count,
                chyby = errors
            });
        }

        private async Task<(bool success, ActionResult response)> CreateOrUpdate(Firma Firma)
        {

            var existujuci = await _context.Firmy
                .FirstOrDefaultAsync(z => z.Kod == Firma.Kod);


            if (!string.IsNullOrWhiteSpace(Firma.RiaditelRc))
            {

                var exists = await _context.Zamestnanci
                    .AnyAsync(z => z.RodneCislo == Firma.RiaditelRc);

                if (!exists)
                    return (false, BadRequest("Rodné číslo neexistuje v tabuľke zamestnanci."));
            }

            var veduciExistuje = await _context.Firmy
            .AnyAsync(d => d.RiaditelRc == Firma.RiaditelRc && d.Kod != Firma.Kod);

            if (veduciExistuje)
            {
                return (false, new ConflictObjectResult(new { sprava = "Riaditeľ nemôže mať viacero firiem." }));
            }

            if (existujuci != null)
            {
                ReplaceValuesOfObject.UpdateNonNullProperties<Firma>(existujuci, Firma, new[] { "Id", "Kod" });
                await _context.SaveChangesAsync();
                return (true, new OkObjectResult(existujuci)); ;
            }


            var (isValid, modelState) = ValidationHelper.ValidateAndHandleModelState(Firma, ModelState);

            if (!isValid)
            {
                return (isValid, new BadRequestObjectResult(modelState));
            }



            _context.Firmy.Add(Firma);
            await _context.SaveChangesAsync();

            return (true, new CreatedAtActionResult(nameof(GetFirma), "Firma", new { kod = Firma.Kod }, Firma));
        }


        [HttpGet("{kod}")]
        public async Task<ActionResult<FirmaDto>> GetFirma(string kod)
        {
            var divizia = await _context.Firmy
                .Where(d => d.Kod == kod)
                .Include(d => d.Divizie)
                .Select(d => new FirmaDto
                {
                    Kod = d.Kod,
                    Nazov = d.Nazov,
                    Divizie = d.Divizie.Select(p => new DiviziaDto
                    {
                        Kod = p.Kod,
                        Nazov = p.Nazov
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (divizia == null)
                return NotFound(new { message = "Firma nebola nájdená." });

            return Ok(divizia);
        }

        [HttpDelete("{kod}")]
        public async Task<ActionResult<Firma>> DeleteFirma(string Kod)
        {
            var firma = await _context.Firmy
                .FirstOrDefaultAsync(z => z.Kod == Kod);

            if (firma == null)
            {
                return NotFound();
            }

            _context.Firmy.Remove(firma);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Firma bola úspešne odstránená." });
        }
    }
}

public class FirmaDto
{
    public int? Id { get; set; }
    public string? Kod { get; set; }
    public string? Nazov { get; set; }
    public List<DiviziaDto>? Divizie { get; set; }

    public string? RiaditelRc { get; set; }  // FK na Zamestnanec.RodneCislo


}

