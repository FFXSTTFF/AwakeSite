using Awake.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Text.Json;

namespace Awake.API.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemDetailsAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden, "Forbidden", ex.Message);
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                title = "Validation Failed",
                status = StatusCodes.Status400BadRequest,
                errors
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, _jsonOptions));
        }
        catch (DomainException ex)
        {
            await WriteProblemDetailsAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            var detail = ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? "Файл слишком большой."
                : "Некорректный запрос.";
            await WriteProblemDetailsAsync(context, ex.StatusCode, "Bad Request", detail);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception: {Message}", ex.Message);
            await WriteProblemDetailsAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error", "Внутренняя ошибка сервера.");
        }
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = $"https://tools.ietf.org/html/rfc9110#section-15.{(statusCode >= 500 ? "6" : "5")}",
            Title = title,
            Status = statusCode,
            Detail = detail
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, _jsonOptions));
    }
}
