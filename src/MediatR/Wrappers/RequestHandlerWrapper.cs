namespace MediatR.Wrappers
{
    using System;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class RequestHandlerBase
    {
        public abstract Task<object?> Handle(object request, CancellationToken cancellationToken,
            ServiceFactory serviceFactory);

        protected static THandler GetHandler<THandler>(ServiceFactory factory)
        {
            THandler handler;

            try
            {
                handler = factory.GetInstance<THandler>();
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Error constructing handler for request of type {typeof(THandler)}. Register your handlers with the container. See the samples in GitHub for examples.", e);
            }

            if (handler == null)
            {
                throw new InvalidOperationException($"Handler was not found for request of type {typeof(THandler)}. Register your handlers with the container. See the samples in GitHub for examples.");
            }

            return handler;
        }
    }

    public abstract class RequestHandlerWrapper<TResponse> : RequestHandlerBase
    {
        public abstract Task<TResponse> Handle(IRequest<TResponse> request, CancellationToken cancellationToken,
            ServiceFactory serviceFactory);
    }

    public class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override async Task<object?> Handle(object request, CancellationToken cancellationToken,
            ServiceFactory serviceFactory) =>
            await Handle((IRequest<TResponse>)request, cancellationToken, serviceFactory).ConfigureAwait(false);

        public override Task<TResponse> Handle(IRequest<TResponse> request, CancellationToken cancellationToken,
            ServiceFactory serviceFactory)
        {
            Task<TResponse> Handler() =>
                GetHandler<IRequestHandler<TRequest, TResponse>>(serviceFactory)
                .Handle((TRequest) request, cancellationToken);

            var behaviours = serviceFactory
                .GetInstances<IPipelineBehavior<TRequest, TResponse>>()
                .Reverse()
                .ToList();

            var rator = behaviours.GetEnumerator();

            var pipeline = (RequestHandlerDelegate<TResponse>)Handler;

            while (rator.MoveNext())
            {
                pipeline = () => rator.Current.Handle((TRequest) request, cancellationToken, pipeline);

            }

            return pipeline();

            //return behaviours
            //    .Aggregate(
            //        (RequestHandlerDelegate<TResponse>) Handler,
            //        (next, pipeline) => () =>
            //            pipeline.Handle((TRequest) request, cancellationToken, next)
            //        )();

        }
    }
}
