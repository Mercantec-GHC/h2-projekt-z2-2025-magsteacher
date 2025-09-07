<script lang="ts">
	import { onMount } from 'svelte';
	import { adminService } from '$lib/services/api';
	import { Calendar, Clock, User, Bed, Building, CheckCircle, XCircle } from 'lucide-svelte';
	import { format } from 'date-fns';
	import { da } from 'date-fns/locale';

	let checkInData: any = null;
	let isLoading = true;
	let error = '';

	onMount(async () => {
		await loadDailyCheckIns();
	});

	async function loadDailyCheckIns() {
		try {
			isLoading = true;
			error = '';
			checkInData = await adminService.getDailyCheckIns();
			
			// Log for debugging
			console.log('Daily check-ins data:', checkInData);
		} catch (err) {
			error = 'Fejl ved hentning af dagens check-ins og check-outs';
			console.error('Error loading daily check-ins:', err);
			
			// Set fallback data for empty state
			checkInData = {
				Date: new Date().toISOString().split('T')[0],
				CheckIns: [],
				CheckOuts: [],
				TotalCheckIns: 0,
				TotalCheckOuts: 0,
				HasAnyActivity: false,
				Message: 'Kunne ikke hente data - prøv igen'
			};
		} finally {
			isLoading = false;
		}
	}

	function formatTime(dateString: string) {
		return format(new Date(dateString), 'HH:mm', { locale: da });
	}

	function formatDate(dateString: string) {
		return format(new Date(dateString), 'dd. MMMM yyyy', { locale: da });
	}
</script>

<div class="bg-white shadow rounded-lg">
	<div class="px-4 py-5 sm:p-6">
		<div class="flex items-center justify-between mb-6">
			<div>
				<h3 class="text-lg leading-6 font-medium text-gray-900">
					<Calendar class="h-5 w-5 inline mr-2" />
					Dagens Check-ins & Check-outs
					{#if checkInData && checkInData.HasAnyActivity}
						<span class="ml-2 inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
							{checkInData.TotalCheckIns + checkInData.TotalCheckOuts} aktiviteter
						</span>
					{/if}
				</h3>
				<p class="mt-1 text-sm text-gray-500">
					{#if checkInData}
						{formatDate(checkInData.Date)}
					{/if}
				</p>
			</div>
			<button
				on:click={loadDailyCheckIns}
				class="inline-flex items-center px-3 py-2 border border-gray-300 shadow-sm text-sm leading-4 font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500"
			>
				<Clock class="h-4 w-4 mr-2" />
				Opdater
			</button>
		</div>

		{#if isLoading}
			<div class="space-y-4">
				<div class="animate-pulse">
					<div class="h-4 bg-gray-200 rounded w-3/4 mb-2"></div>
					<div class="h-4 bg-gray-200 rounded w-1/2"></div>
				</div>
				<div class="animate-pulse">
					<div class="h-4 bg-gray-200 rounded w-3/4 mb-2"></div>
					<div class="h-4 bg-gray-200 rounded w-1/2"></div>
				</div>
			</div>
		{:else if error}
			<div class="text-center py-6">
				<XCircle class="h-12 w-12 text-red-400 mx-auto mb-4" />
				<p class="text-sm text-red-600">{error}</p>
				<button
					on:click={loadDailyCheckIns}
					class="mt-2 text-sm text-indigo-600 hover:text-indigo-500"
				>
					Prøv igen
				</button>
			</div>
		{:else if checkInData}
			<!-- Empty day message -->
			{#if !checkInData.HasAnyActivity}
				<div class="text-center py-12">
					<div class="mx-auto h-16 w-16 bg-gray-100 rounded-full flex items-center justify-center mb-4">
						<Calendar class="h-8 w-8 text-gray-400" />
					</div>
					<h3 class="text-lg font-medium text-gray-900 mb-2">Ingen aktivitet i dag</h3>
					<p class="text-sm text-gray-500 mb-4">
						{checkInData.Message || 'Ingen check-ins eller check-outs planlagt for i dag'}
					</p>
					<div class="text-xs text-gray-400">
						{formatDate(checkInData.Date)}
					</div>
				</div>
			{:else}
				<div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
					<!-- Check-ins -->
					<div>
						<div class="flex items-center mb-4">
							<CheckCircle class="h-5 w-5 text-green-500 mr-2" />
							<h4 class="text-md font-medium text-gray-900">Check-ins ({checkInData.TotalCheckIns})</h4>
						</div>
						
						{#if checkInData.CheckIns.length === 0}
							<div class="text-center py-4">
								<Calendar class="h-8 w-8 text-gray-400 mx-auto mb-2" />
								<p class="text-sm text-gray-500">Ingen check-ins i dag</p>
							</div>
						{:else}
						<div class="space-y-3">
							{#each checkInData.CheckIns as checkIn}
								<div class="border border-gray-200 rounded-lg p-4 hover:bg-gray-50 transition-colors">
									<div class="flex items-start justify-between">
										<div class="flex-1">
											<div class="flex items-center mb-2">
												<User class="h-4 w-4 text-gray-400 mr-2" />
												<span class="text-sm font-medium text-gray-900">{checkIn.UserName}</span>
											</div>
											<div class="flex items-center text-sm text-gray-600 mb-1">
												<Bed class="h-4 w-4 text-gray-400 mr-2" />
												Værelse {checkIn.RoomNumber}
											</div>
											<div class="flex items-center text-sm text-gray-600 mb-2">
												<Building class="h-4 w-4 text-gray-400 mr-2" />
												{checkIn.HotelName}
											</div>
											<div class="flex items-center text-sm text-gray-500">
												<Clock class="h-4 w-4 text-gray-400 mr-2" />
												Check-in: {formatTime(checkIn.CheckInTime)}
											</div>
										</div>
										<div class="text-right">
											<span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
												{checkIn.Duration} {checkIn.Duration === 1 ? 'dag' : 'dage'}
											</span>
										</div>
									</div>
								</div>
							{/each}
						</div>
					{/if}
				</div>

				<!-- Check-outs -->
				<div>
					<div class="flex items-center mb-4">
						<XCircle class="h-5 w-5 text-red-500 mr-2" />
						<h4 class="text-md font-medium text-gray-900">Check-outs ({checkInData.TotalCheckOuts})</h4>
					</div>
					
					{#if checkInData.CheckOuts.length === 0}
						<div class="text-center py-4">
							<Calendar class="h-8 w-8 text-gray-400 mx-auto mb-2" />
							<p class="text-sm text-gray-500">Ingen check-outs i dag</p>
						</div>
					{:else}
						<div class="space-y-3">
							{#each checkInData.CheckOuts as checkOut}
								<div class="border border-gray-200 rounded-lg p-4 hover:bg-gray-50 transition-colors">
									<div class="flex items-start justify-between">
										<div class="flex-1">
											<div class="flex items-center mb-2">
												<User class="h-4 w-4 text-gray-400 mr-2" />
												<span class="text-sm font-medium text-gray-900">{checkOut.UserName}</span>
											</div>
											<div class="flex items-center text-sm text-gray-600 mb-1">
												<Bed class="h-4 w-4 text-gray-400 mr-2" />
												Værelse {checkOut.RoomNumber}
											</div>
											<div class="flex items-center text-sm text-gray-600 mb-2">
												<Building class="h-4 w-4 text-gray-400 mr-2" />
												{checkOut.HotelName}
											</div>
											<div class="flex items-center text-sm text-gray-500">
												<Clock class="h-4 w-4 text-gray-400 mr-2" />
												Check-out: {formatTime(checkOut.CheckOutTime)}
											</div>
										</div>
										<div class="text-right">
											<span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-red-100 text-red-800">
												{checkOut.Duration} {checkOut.Duration === 1 ? 'dag' : 'dage'}
											</span>
										</div>
									</div>
								</div>
							{/each}
						</div>
					{/if}
				</div>
			</div>

					<!-- Summary Stats -->
					{#if checkInData.TotalCheckIns > 0 || checkInData.TotalCheckOuts > 0}
						<div class="mt-6 pt-6 border-t border-gray-200">
							<div class="grid grid-cols-2 gap-4">
								<div class="text-center">
									<div class="text-2xl font-bold text-green-600">{checkInData.TotalCheckIns}</div>
									<div class="text-sm text-gray-500">Check-ins i dag</div>
								</div>
								<div class="text-center">
									<div class="text-2xl font-bold text-red-600">{checkInData.TotalCheckOuts}</div>
									<div class="text-sm text-gray-500">Check-outs i dag</div>
								</div>
							</div>
						</div>
					{/if}
				</div>
			{/if}
		{/if}
	</div>
</div>
