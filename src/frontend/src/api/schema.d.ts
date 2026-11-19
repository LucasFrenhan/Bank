// Arquivo gerado por `npm run api:tipos` a partir de /openapi/v1.json. Não editar.
export interface paths {
    "/health": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Indica se a API está em execução. */
        get: operations["ConsultarSaude"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/autenticacao/registrar": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Cadastra um usuário PF ou PJ e cria sua carteira. */
        post: operations["Registrar"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/autenticacao/login": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Valida as credenciais e emite o token de acesso (JWT). */
        post: operations["Login"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/carteiras/minha": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Retorna a carteira e o saldo atual do usuário autenticado. */
        get: operations["ObterMinhaCarteira"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/usuarios/me": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Retorna os dados cadastrais do usuário autenticado. */
        get: operations["ObterTitular"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /**
         * Transfere da carteira do usuário (PF) para uma conta PF ou PJ, com idempotência.
         * @description Reenvios com o mesmo cabeçalho Idempotency-Key devolvem 200 com o comprovante original, sem novo débito. Valor acima do limite por operação ou do limite diário (dia de Brasília) recebe 422 limite-excedido, com tipoLimite, limite e valorDisponivel.
         */
        post: operations["Transferir"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias/destinatarios/consultar": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Confirma o nome e o tipo de conta do destinatário pelo documento, antes de transferir. */
        post: operations["ConsultarDestinatario"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias/extrato": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Lista os lançamentos da carteira do usuário (PF e PJ), do mais recente para o mais antigo. */
        get: operations["ObterExtrato"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias/limites": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Limites de transferência da PF e quanto ainda pode ser enviado hoje (dia de Brasília).
         * @description Informativo: o envio verifica os limites de novo. Limites e disponível nulos = sem limite.
         */
        get: operations["ConsultarLimites"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transferencias/{transferenciaId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Reabre o comprovante de uma transferência (pagador ou recebedor).
         * @description Para o recebedor, chaveIdempotencia vem nula. Transferência inexistente ou de que o usuário não participa recebe o mesmo 404.
         */
        get: operations["ConsultarTransferencia"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        ComprovanteResposta: {
            /** Format: uuid */
            transferenciaId: string;
            /** Format: uuid */
            chaveIdempotencia: null | string;
            pagador: components["schemas"]["PagadorResposta"];
            recebedor: components["schemas"]["RecebedorResposta"];
            /** Format: double */
            valor: number | string;
            descricao: null | string;
            status: string;
            /** Format: date-time */
            criadoEm: string;
        };
        ConsultaDestinatarioRequisicao: {
            documento: null | string;
        };
        ContraparteResposta: {
            nome: string;
            documento: string;
            tipoConta: string;
        };
        DestinatarioResposta: {
            nome: string;
            tipoConta: string;
        };
        ExtratoResposta: {
            itens: components["schemas"]["ItemExtratoResposta"][];
            /** Format: int32 */
            paginaAtual: number | string;
            /** Format: int32 */
            tamanhoPagina: number | string;
            /** Format: int32 */
            totalItens: number | string;
        };
        HttpValidationProblemDetails: {
            type?: null | string;
            title?: null | string;
            /** Format: int32 */
            status?: null | number | string;
            detail?: null | string;
            instance?: null | string;
            errors?: {
                [key: string]: string[];
            };
        };
        ItemExtratoResposta: {
            /** Format: uuid */
            lancamentoId: string;
            tipo: string;
            direcao: string;
            /** Format: uuid */
            transferenciaId: null | string;
            contraparte: null | components["schemas"]["ContraparteResposta"];
            /** Format: double */
            valor: number | string;
            descricao: null | string;
            /** Format: date-time */
            criadoEm: string;
        };
        LimitesResposta: {
            /** Format: double */
            porOperacao: null | number | string;
            /** Format: double */
            diario: null | number | string;
            /** Format: double */
            transferidoHoje: number | string;
            /** Format: double */
            disponivelHoje: null | number | string;
            /** Format: date */
            dia: string;
        };
        LoginRequisicao: {
            email: null | string;
            senha: null | string;
        };
        LoginResposta: {
            tokenAcesso: string;
            tipoToken: string;
            /** Format: int32 */
            expiraEmSegundos: number | string;
        };
        MinhaCarteiraResposta: {
            /** Format: uuid */
            carteiraId: string;
            /** Format: uuid */
            usuarioId: string;
            nomeTitular: string;
            tipoConta: string;
            /** Format: double */
            saldo: number | string;
            moeda: string;
            /** Format: date-time */
            atualizadoEm: string;
        };
        PagadorResposta: {
            /** Format: uuid */
            usuarioId: string;
            nome: string;
            documento: string;
        };
        ProblemDetails: {
            type?: null | string;
            title?: null | string;
            /** Format: int32 */
            status?: null | number | string;
            detail?: null | string;
            instance?: null | string;
        };
        RecebedorResposta: {
            /** Format: uuid */
            usuarioId: string;
            nome: string;
            documento: string;
            tipoConta: string;
        };
        RegistrarRequisicao: {
            nomeCompleto: null | string;
            email: null | string;
            documento: null | string;
            tipoConta: null | string;
            senha: null | string;
        };
        TitularResposta: {
            /** Format: uuid */
            usuarioId: string;
            nomeCompleto: string;
            email: string;
            documento: string;
            tipoConta: string;
            /** Format: date-time */
            criadoEm: string;
        };
        TransferenciaRequisicao: {
            documentoDestino: null | string;
            /** Format: double */
            valor: null | number | string;
            descricao: null | string;
        };
        UsuarioRegistradoResposta: {
            /** Format: uuid */
            usuarioId: string;
            /** Format: uuid */
            carteiraId: string;
            nomeCompleto: string;
            email: string;
            documento: string;
            tipoConta: string;
            /** Format: double */
            saldoInicial: number | string;
            moeda: string;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
    ConsultarSaude: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "text/plain": string;
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "text/plain": string;
                };
            };
        };
    };
    Registrar: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["RegistrarRequisicao"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["UsuarioRegistradoResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    Login: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["LoginRequisicao"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["LoginResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ObterMinhaCarteira: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["MinhaCarteiraResposta"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ObterTitular: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["TitularResposta"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    Transferir: {
        parameters: {
            query?: never;
            header: {
                /** @description UUID gerado pelo cliente; reenvios com a mesma chave não debitam de novo. */
                "Idempotency-Key": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["TransferenciaRequisicao"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ComprovanteResposta"];
                };
            };
            /** @description Created */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ComprovanteResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ConsultarDestinatario: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ConsultaDestinatarioRequisicao"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["DestinatarioResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ObterExtrato: {
        parameters: {
            query?: {
                pagina?: string;
                tamanhoPagina?: string;
                direcao?: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ExtratoResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ConsultarLimites: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["LimitesResposta"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ConsultarTransferencia: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                transferenciaId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ComprovanteResposta"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["HttpValidationProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
}
