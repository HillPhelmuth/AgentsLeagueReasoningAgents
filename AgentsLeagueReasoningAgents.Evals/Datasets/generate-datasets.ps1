param(
    [int]$CuratorCount = 30,
    [int]$PlannerCount = 30,
    [int]$EngagementCount = 20,
    [int]$AssessmentCount = 40,
    [int]$ScenarioCount = 30
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$seedRoot = Join-Path $root 'SeedSessions'
$commonDir = Join-Path $root 'common'
$curatorDir = Join-Path $root 'curator'
$plannerDir = Join-Path $root 'planner'
$engagementDir = Join-Path $root 'engagement'
$assessmentDir = Join-Path $root 'assessment'

@($commonDir, $curatorDir, $plannerDir, $engagementDir, $assessmentDir) | ForEach-Object {
    if (-not (Test-Path $_)) { New-Item -ItemType Directory -Path $_ | Out-Null }
}

$topicFamilies = @('AZ-900','AZ-104','AI-102','SC-900','DP-900','AZ-305','AZ-204','PL-300')
$levels = @('beginner','intermediate','advanced')
$durations = @('short','standard','aggressive')
$difficulties = @('easy','medium','hard')
$qualityBands = @('excellent','good','mixed','poor')

$requiredEvals = @(
    'IntentResolutionExplain',
    'ToolCallAccuracyExplain',
    'TaskAdherenceExplain',
    'RelevanceExplain',
    'CoherenceExplain',
    'PerceivedIntelligenceExplain',
    'FluencyExplain',
    'EmpathyExplain',
    'HelpfulnessExplain'
)

$agentToSeedFolder = @{
    'learning-path-curator' = 'learning-path-curator'
    'study-plan-generator' = 'study-plan-generator'
    'engagement-agent' = 'engagement-agent'
    'readiness-assessment-agent' = 'readiness-assessment-agent'
}

$agentContracts = @{
    'learning-path-curator' = 'LearningPathCurationOutput'
    'study-plan-generator' = 'StudyPlanOutput'
    'engagement-agent' = 'EngagementPlanOutput'
    'readiness-assessment-agent' = 'AssessmentQuestionSetOutput'
}

$agentGoals = @{
    'learning-path-curator' = 'Return JSON only matching the learning path curation schema with rationale, learning paths, modules, and targets.'
    'study-plan-generator' = 'Return JSON only matching the study plan schema with weekly milestones and practical daily sessions.'
    'engagement-agent' = 'Return JSON only matching the engagement schema with reminders and motivational messages aligned to the plan.'
    'readiness-assessment-agent' = 'Return JSON only matching the assessment schema with exactly 10 MCQs (A-D), one correct option id, and explanation.'
}

function New-Scenario([int]$idx) {
    $topic = $topicFamilies[$idx % $topicFamilies.Count]
    $level = $levels[$idx % $levels.Count]
    $duration = $durations[$idx % $durations.Count]
    $hours = 4 + ($idx % 8)
    $weeks = 4 + ($idx % 10)
    [ordered]@{
        scenario_id = ('SCN-{0:000}' -f $idx)
        topic_family = $topic
        learner_level = $level
        duration_profile = $duration
        weekly_hours = $hours
        duration_weeks = $weeks
        ambiguity = @('clear','underspecified','conflicting')[$idx % 3]
        noise_factor = @('none','outdated-links','irrelevant-forum-advice','promo-voucher-distraction')[$idx % 4]
        target_exam = $topic
    }
}

function ConvertTo-Hashtable($obj) {
    if ($null -eq $obj) { return $null }

    if ($obj -is [System.Collections.IDictionary]) {
        $ht = @{}
        foreach ($key in $obj.Keys) {
            $ht[$key] = ConvertTo-Hashtable $obj[$key]
        }
        return $ht
    }

    if ($obj -is [System.Collections.IEnumerable] -and -not ($obj -is [string])) {
        $list = @()
        foreach ($item in $obj) {
            $list += ,(ConvertTo-Hashtable $item)
        }
        return $list
    }

    if ($obj -is [pscustomobject]) {
        $ht = @{}
        foreach ($prop in $obj.PSObject.Properties) {
            $ht[$prop.Name] = ConvertTo-Hashtable $prop.Value
        }
        return $ht
    }

    return $obj
}

function ConvertTo-DeepClone($obj) {
    if ($null -eq $obj) { return $null }
    return ConvertTo-Hashtable $obj
}

function Get-SeedSessionsForAgent([string]$agentName) {
    $seedFolder = Join-Path $seedRoot $agentToSeedFolder[$agentName]
    if (-not (Test-Path $seedFolder)) {
        throw "Missing SeedSessions folder for ${agentName}: $seedFolder"
    }

    $files = Get-ChildItem -Path $seedFolder -Filter '*.json' -File | Sort-Object Name
    if (-not $files) {
        throw "No seed session files found for $agentName in $seedFolder"
    }

    $sessions = @()
    foreach ($file in $files) {
        try {
            $raw = Get-Content -Path $file.FullName -Raw -Encoding UTF8
            $doc = $raw | ConvertFrom-Json
            $messages = $doc.stateBag.InMemoryChatHistoryProvider.messages
            if (-not $messages) { continue }

            $firstUser = $messages | Where-Object { $_.role -eq 'user' } | Select-Object -First 1
            $userText = ''
            if ($firstUser -and $firstUser.contents) {
                $userText = ($firstUser.contents | ForEach-Object { $_.text } | Where-Object { $_ } | Select-Object -First 1)
            }

            $assistantMessages = $messages | Where-Object { $_.role -eq 'assistant' }
            $finalAssistantText = $null
            foreach ($msg in ($assistantMessages | Select-Object -Last 4)) {
                foreach ($content in $msg.contents) {
                    if ($content.'$type' -eq 'text' -and $content.text) {
                        $finalAssistantText = $content.text
                    }
                }
            }

            if (-not $finalAssistantText) { continue }

            $outputObject = $null
            try {
                $outputObject = $finalAssistantText | ConvertFrom-Json
            }
            catch {
                continue
            }

            $functionCalls = @()
            foreach ($msg in $assistantMessages) {
                foreach ($content in $msg.contents) {
                    if ($content.'$type' -eq 'functionCall') {
                        $functionCalls += [ordered]@{
                            callId = $content.callId
                            tool = $content.name
                            arguments = ConvertTo-Hashtable $content.arguments
                        }
                    }
                }
            }

            $functionResultsById = @{}
            foreach ($toolMsg in ($messages | Where-Object { $_.role -eq 'tool' })) {
                foreach ($content in $toolMsg.contents) {
                    if ($content.'$type' -eq 'functionResult' -and $content.callId) {
                        $functionResultsById[$content.callId] = $content.result
                    }
                }
            }

            $invokedCalls = @()
            foreach ($call in $functionCalls) {
                $result = $null
                if ($functionResultsById.ContainsKey($call.callId)) {
                    $result = $functionResultsById[$call.callId]
                }
                $resultText = if ($result -is [string]) { $result } else { ($result | ConvertTo-Json -Depth 8 -Compress) }
                $outcome = if ($resultText -match 'Error|failed') { 'error' } else { 'relevant' }
                $invokedCalls += [ordered]@{
                    tool = $call.tool
                    arguments = $call.arguments
                    outcome = $outcome
                }
            }

            $availableTools = @($functionCalls | ForEach-Object { $_.tool } | Where-Object { $_ } | Select-Object -Unique)
            $expectedCalls = @($invokedCalls | Select-Object -First ([Math]::Min(2, $invokedCalls.Count)))

            $sessions += [ordered]@{
                source_file = $file.Name
                user_prompt = $userText
                output_template = ConvertTo-DeepClone $outputObject
                available_tools = $availableTools
                expected_tool_calls = ConvertTo-DeepClone $expectedCalls
                invoked_tool_calls = ConvertTo-DeepClone $invokedCalls
            }
        }
        catch {
            continue
        }
    }

    if (-not $sessions) {
        throw "Unable to parse any valid seed sessions for $agentName"
    }

    return $sessions
}

function New-Reminder([datetime]$start, [int]$offsetDays, [string]$subject, [string]$body, [string]$link) {
    return [ordered]@{
        schedule = $start.AddDays($offsetDays).ToString('yyyy-MM-ddTHH:mm:ss')
        subject = $subject
        body = $body
        link = $link
    }
}

function New-Motivation([datetime]$start, [int]$offsetDays, [string]$body) {
    return [ordered]@{
        schedule = $start.AddDays($offsetDays).ToString('yyyy-MM-ddTHH:mm:ss')
        body = $body
    }
}

function New-AssessmentQuestion([int]$n, [string]$topic) {
    [ordered]@{
        questionId = "Q$n"
        prompt = "Which choice best supports a $topic exam prep scenario with grounded, verifiable outputs?"
        options = @(
            [ordered]@{ optionId = 'A'; text = 'Use retrieved sources and require concise citations in the answer.' },
            [ordered]@{ optionId = 'B'; text = 'Answer from memory only and avoid references to improve speed.' },
            [ordered]@{ optionId = 'C'; text = 'Skip validation and focus on output length.' },
            [ordered]@{ optionId = 'D'; text = 'Ignore learner constraints and return generic advice.' }
        )
        correctOptionId = 'A'
        explanation = 'Grounded retrieval and concise citations improve relevance, trust, and exam readiness.'
    }
}

function Apply-ScenarioToOutput([string]$agentName, $template, $scenario, [int]$idx, [string]$quality) {
    $output = ConvertTo-DeepClone $template

    switch ($agentName) {
        'learning-path-curator' {
            $output.rationale = "Recommendations are aligned to $($scenario.topic_family) for a $($scenario.learner_level) learner with $($scenario.weekly_hours)h/week over $($scenario.duration_weeks) weeks, prioritizing Microsoft Learn resources and exam relevance."
            if ($output.targets -and $output.targets.Count -gt 0) {
                $output.targets[0].title = "Exam $($scenario.topic_family) readiness track"
            }
            if ($quality -eq 'poor') {
                $output.modules = @($output.modules | Select-Object -First 1)
                $output.rationale = "Basic path shortlist for $($scenario.topic_family)."
            }
        }
        'study-plan-generator' {
            $output.durationWeeks = [int]$scenario.duration_weeks
            $output.weeklyHours = [int]$scenario.weekly_hours
            $output.planTitle = "$($scenario.topic_family) study plan - $($scenario.duration_weeks) weeks"
            if ($output.briefMessageToUser) {
                $output.briefMessageToUser = "Stay consistent: $($scenario.weekly_hours) hours per week and complete each weekly checkpoint."
            }
            if ($quality -eq 'mixed' -and $output.weeklyMilestones.Count -gt 4) {
                $output.weeklyMilestones = @($output.weeklyMilestones | Select-Object -First 4)
            }
            if ($quality -eq 'poor') {
                $output.rationale = "Compressed schedule generated from prior plan patterns."
                if ($output.weeklyMilestones.Count -gt 2) {
                    $output.weeklyMilestones = @($output.weeklyMilestones | Select-Object -First 2)
                }
            }
        }
        'engagement-agent' {
            $email = "student+$idx@example.com"
            $output.recipientEmail = $email
            $start = [datetime]'2026-03-02T18:30:00'

            $output.reminders = @(
                (New-Reminder -start $start -offsetDays 0 -subject "Week 1: $($scenario.topic_family) kickoff" -body "Start the first study session and capture baseline notes." -link 'https://learn.microsoft.com/training/'),
                (New-Reminder -start $start -offsetDays 2 -subject "Midweek checkpoint" -body "Complete planned sessions and update weak areas." -link 'https://learn.microsoft.com/credentials/'),
                (New-Reminder -start $start -offsetDays 5 -subject "Weekly review" -body "Run a short self-check and prepare next week objectives." -link 'https://learn.microsoft.com/training/')
            )
            $output.motivationMessages = @(
                (New-Motivation -start $start -offsetDays 1 -body 'Consistency beats intensity. Finish today''s session and capture one takeaway.'),
                (New-Motivation -start $start -offsetDays 4 -body 'Small daily progress compounds. Keep your study streak active this week.')
            )
            if ($quality -eq 'poor') {
                $output.motivationMessages = @($output.motivationMessages | Select-Object -First 1)
            }
        }
        'readiness-assessment-agent' {
            $output.introMessage = "Answer 10 multiple-choice questions to check $($scenario.topic_family) readiness."
            $output.questions = @()
            for ($q = 1; $q -le 10; $q++) {
                $output.questions += ,(New-AssessmentQuestion -n $q -topic $scenario.topic_family)
            }
            if ($quality -eq 'poor') {
                $output.questions[0].explanation = 'Grounded answers are generally better for reliability.'
            }
        }
    }

    return $output
}

function New-AgentCase($agentName, $contract, $phase, $profile, $scenario, $seed, [int]$idx) {
    $difficulty = $difficulties[$idx % $difficulties.Count]
    $quality = $qualityBands[$idx % $qualityBands.Count]

    $question = if ([string]::IsNullOrWhiteSpace($seed.user_prompt)) {
        "Create output for $($scenario.topic_family) learner ($($scenario.learner_level)) with $($scenario.weekly_hours) hours/week over $($scenario.duration_weeks) weeks."
    }
    else {
        ($seed.user_prompt -replace '\r?\n+', ' ').Trim()
    }

    $context = "Scenario $($scenario.scenario_id): ambiguity=$($scenario.ambiguity), noise=$($scenario.noise_factor), target exam=$($scenario.target_exam)."
    $goal = $agentGoals[$agentName]

    $modelObject = Apply-ScenarioToOutput -agentName $agentName -template $seed.output_template -scenario $scenario -idx $idx -quality $quality
    $referenceObject = Apply-ScenarioToOutput -agentName $agentName -template $seed.output_template -scenario $scenario -idx ($idx + 1000) -quality 'excellent'

    $answer = $modelObject | ConvertTo-Json -Depth 100 -Compress
    $reference = $referenceObject | ConvertTo-Json -Depth 100 -Compress

    $toolData = @{
        available_tools = if ($seed.available_tools) { $seed.available_tools } else { @() }
        expected_tool_calls = if ($seed.expected_tool_calls) { $seed.expected_tool_calls } else { @() }
        invoked_tool_calls = if ($seed.invoked_tool_calls) { $seed.invoked_tool_calls } else { @() }
    }

    $explainInputs = [ordered]@{
        RelevanceExplain = [ordered]@{ input = $answer; question = $question; context = $context }
        CoherenceExplain = [ordered]@{ input = $answer; question = $question }
        PerceivedIntelligenceExplain = [ordered]@{ input = $answer; question = $question; context = $context; rag_mode = 'non-rag' }
        FluencyExplain = [ordered]@{ input = $answer; question = $question }
        EmpathyExplain = [ordered]@{ input = $answer; question = $question }
        HelpfulnessExplain = [ordered]@{ input = $answer; question = $question }
        IntentResolutionExplain = [ordered]@{ input = $answer; question = $question; relevantContext = $context }
        ToolCallAccuracyExplain = [ordered]@{ input = $answer; question = $question; availableTools = $toolData.available_tools; invokedTools = $toolData.invoked_tool_calls }
        TaskAdherenceExplain = [ordered]@{ input = $answer; question = $question; goal = $goal }
    }

    [ordered]@{
        case_id = "{0}-{1:000}" -f $agentName, $idx
        scenario_id = $scenario.scenario_id
        agent_name = $agentName
        phase = $phase
        difficulty = $difficulty
        quality_band = $quality
        topic_family = $scenario.topic_family
        learner_level = $scenario.learner_level
        question = $question
        context = $context
        model_answer = $answer
        reference_answer = $reference
        task_goal = $goal
        relevant_context = $context
        available_tools = $toolData.available_tools
        expected_tool_calls = $toolData.expected_tool_calls
        invoked_tool_calls = $toolData.invoked_tool_calls
        explain_inputs = $explainInputs
        required_evals = $requiredEvals
        threshold_profile = $profile
        expected_contract = $contract
    }
}

$scenarios = for ($i = 1; $i -le $ScenarioCount; $i++) { New-Scenario -idx $i }

$seedSessionsByAgent = @{}
foreach ($agent in $agentToSeedFolder.Keys) {
    $seedSessionsByAgent[$agent] = @(Get-SeedSessionsForAgent -agentName $agent)
}

$scenarioFile = Join-Path $commonDir 'scenario-catalog.jsonl'
$scenarioLines = $scenarios | ForEach-Object { $_ | ConvertTo-Json -Depth 8 -Compress }
Set-Content -Path $scenarioFile -Value $scenarioLines -Encoding UTF8

function Write-AgentDataset($path, $agentName, $phase, $profile, [int]$count) {
    $contract = $agentContracts[$agentName]
    $seeds = $seedSessionsByAgent[$agentName]

    $rows = for ($i = 1; $i -le $count; $i++) {
        $scenario = $scenarios[($i - 1) % $scenarios.Count]
        $seed = $seeds[($i - 1) % $seeds.Count]
        New-AgentCase -agentName $agentName -contract $contract -phase $phase -profile $profile -scenario $scenario -seed $seed -idx $i
    }

    $lines = $rows | ForEach-Object { $_ | ConvertTo-Json -Depth 16 -Compress }
    Set-Content -Path $path -Value $lines -Encoding UTF8
}

Write-AgentDataset -path (Join-Path $curatorDir 'learning-path-curator.explain.jsonl') -agentName 'learning-path-curator' -phase 'preparation' -profile 'prep_default' -count $CuratorCount
Write-AgentDataset -path (Join-Path $plannerDir 'study-plan-generator.explain.jsonl') -agentName 'study-plan-generator' -phase 'preparation' -profile 'prep_default' -count $PlannerCount
Write-AgentDataset -path (Join-Path $engagementDir 'engagement-agent.explain.jsonl') -agentName 'engagement-agent' -phase 'preparation' -profile 'prep_default' -count $EngagementCount
Write-AgentDataset -path (Join-Path $assessmentDir 'readiness-assessment-agent.explain.jsonl') -agentName 'readiness-assessment-agent' -phase 'assessment' -profile 'assessment_strict' -count $AssessmentCount

$total = $CuratorCount + $PlannerCount + $EngagementCount + $AssessmentCount
Write-Host "Generated scenario catalog: $scenarioFile"
Write-Host "Generated agent datasets from SeedSessions with total cases: $total"
