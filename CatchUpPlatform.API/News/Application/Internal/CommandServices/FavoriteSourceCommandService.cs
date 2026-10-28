using CatchUpPlatform.API.News.Application.Errors;
using CatchUpPlatform.API.News.Application.Services;
using CatchUpPlatform.API.News.Domain.Model.Aggregates;
using CatchUpPlatform.API.News.Domain.Model.Commands;
using CatchUpPlatform.API.News.Domain.Repositories;
using CatchUpPlatform.API.Shared.Application.Patterns;
using CatchUpPlatform.API.Shared.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CatchUpPlatform.API.News.Application.Internal.CommandServices;

public class FavoriteSourceCommandService(
    IFavoriteSourceRepository favoriteSourceRepository,
    IUnitOfWork unitOfWork,
    ILogger<FavoriteSourceCommandService> logger
    ): IFavoriteSourceCommandService
{
    public async Task<Result<FavoriteSource, CreateFavoriteSourceError>> Handle(CreateFavoriteSourceCommand command, CancellationToken cancellationToken = default)
    {
        var existingSource = await favoriteSourceRepository.FindByNewsApiKeyAndSourceIdAsync(command.NewsApiKey, command.SourceId, cancellationToken);
        if (existingSource != null)
        {
            logger.LogWarning("Duplicate favorite source creation attempt for NewsApiKey: {NewsApiKey}, SourceId: {SourceId}", command.NewsApiKey.MaskedForLogging(), command.SourceId);
            return new Result<FavoriteSource, CreateFavoriteSourceError>.Failure(CreateFavoriteSourceError.DuplicateFavoriteSource);
        }

        try
        {
            var favoriteSource = new FavoriteSource(command);
            await favoriteSourceRepository.AddAsync(favoriteSource, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
            return new Result<FavoriteSource, CreateFavoriteSourceError>.Success(favoriteSource);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex,
                "Invalid argument when creating favorite source for NewsApiKey: {NewsApiKey}, SourceId: {SourceId}",
                command.NewsApiKey.MaskedForLogging(), command.SourceId);

            return new Result<FavoriteSource, CreateFavoriteSourceError>.Failure(CreateFavoriteSourceError
                .UnexpectedError);
        }
        catch (DbUpdateException e)
        {
            logger.LogWarning(e,
                "Database update failed creating favorite source for NewsApiKey: {NewsApiKey}, SourceId: {SourceId}",
                command.NewsApiKey.MaskedForLogging(), command.SourceId);

            return new Result<FavoriteSource, CreateFavoriteSourceError>.Failure(CreateFavoriteSourceError
                .UnexpectedError);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "Unexpected error creating favorite source for NewsApiKey: {NewsApiKey}, SourceId: {SourceId}",
                command.NewsApiKey.MaskedForLogging(), command.SourceId);

            return new Result<FavoriteSource, CreateFavoriteSourceError>.Failure(CreateFavoriteSourceError
                .UnexpectedError);
        }
    }
}