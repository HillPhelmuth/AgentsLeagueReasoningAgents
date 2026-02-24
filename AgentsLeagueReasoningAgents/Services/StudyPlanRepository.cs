using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Cosmos;

namespace AgentsLeagueReasoningAgents.Services;

public class StudyPlanRepository(CosmosClient cosmosClient)
{
    private readonly Container _studyPlanContainer = cosmosClient?.GetContainer("agent-league-db", "study-plan-container") ?? throw new ArgumentNullException(nameof(cosmosClient));
}