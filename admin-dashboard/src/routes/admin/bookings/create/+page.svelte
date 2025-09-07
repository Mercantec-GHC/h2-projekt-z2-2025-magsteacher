<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { createBooking } from '$lib/stores/bookings';
	import { loadRooms } from '$lib/stores/rooms';
	import { loadUsers } from '$lib/stores/users';
	import { validateBookingForm } from '$lib/utils/validation';
	import { Calendar, User, Bed, Save, ArrowLeft } from 'lucide-svelte';

	let formData = {
		userId: '',
		roomId: '',
		startDate: '',
		endDate: ''
	};

	let errors: Record<string, string> = {};
	let isSubmitting = false;
	let users: any[] = [];
	let rooms: any[] = [];

	onMount(async () => {
		await Promise.all([
			loadRooms(),
			loadUsers()
		]);
		
		// Set default dates
		const today = new Date();
		const tomorrow = new Date(today);
		tomorrow.setDate(tomorrow.getDate() + 1);
		
		formData.startDate = today.toISOString().split('T')[0];
		formData.endDate = tomorrow.toISOString().split('T')[0];
	});

	async function handleSubmit() {
		errors = {};
		
		const validation = validateBookingForm(formData);
		if (!validation.isValid) {
			errors = validation.errors;
			return;
		}

		isSubmitting = true;
		
		try {
			const result = await createBooking(formData);
			
			if (result.success) {
				goto('/admin/bookings');
			} else {
				errors.general = result.error || 'Fejl ved oprettelse af booking';
			}
		} catch (error) {
			errors.general = 'Der opstod en uventet fejl';
		} finally {
			isSubmitting = false;
		}
	}
</script>

<svelte:head>
	<title>Opret Booking - Receptionist Dashboard</title>
</svelte:head>

<div class="space-y-6">
	<!-- Header -->
	<div class="md:flex md:items-center md:justify-between">
		<div class="flex-1 min-w-0">
			<nav class="flex" aria-label="Breadcrumb">
				<ol class="flex items-center space-x-4">
					<li>
						<div>
							<a href="/admin/bookings" class="text-gray-400 hover:text-gray-500">
								<ArrowLeft class="h-5 w-5" />
								<span class="sr-only">Tilbage</span>
							</a>
						</div>
					</li>
					<li>
						<div class="flex items-center">
							<span class="text-gray-400">/</span>
							<span class="ml-4 text-sm font-medium text-gray-500">Bookings</span>
						</div>
					</li>
					<li>
						<div class="flex items-center">
							<span class="text-gray-400">/</span>
							<span class="ml-4 text-sm font-medium text-gray-900">Opret</span>
						</div>
					</li>
				</ol>
			</nav>
			<h1 class="mt-2 text-2xl font-bold leading-7 text-gray-900 sm:truncate sm:text-3xl sm:tracking-tight">
				Opret Ny Booking
			</h1>
		</div>
	</div>

	<!-- Form -->
	<div class="bg-white shadow sm:rounded-lg">
		<form on:submit|preventDefault={handleSubmit} class="px-4 py-5 sm:p-6 space-y-6">
			{#if errors.general}
				<div class="rounded-md bg-red-50 p-4">
					<div class="flex">
						<div class="flex-shrink-0">
							<svg class="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
								<path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.28 7.22a.75.75 0 00-1.06 1.06L8.94 10l-1.72 1.72a.75.75 0 101.06 1.06L10 11.06l1.72 1.72a.75.75 0 101.06-1.06L11.06 10l1.72-1.72a.75.75 0 00-1.06-1.06L10 8.94 8.28 7.22z" clip-rule="evenodd" />
							</svg>
						</div>
						<div class="ml-3">
							<h3 class="text-sm font-medium text-red-800">
								Fejl
							</h3>
							<div class="mt-2 text-sm text-red-700">
								<p>{errors.general}</p>
							</div>
						</div>
					</div>
				</div>
			{/if}

			<div class="grid grid-cols-1 gap-6 sm:grid-cols-2">
				<!-- User Selection -->
				<div>
					<label for="userId" class="block text-sm font-medium text-gray-700">
						Bruger
					</label>
					<div class="mt-1 relative">
						<div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
							<User class="h-5 w-5 text-gray-400" />
						</div>
						<select
							id="userId"
							name="userId"
							class="block w-full pl-10 pr-10 py-2 text-base border-gray-300 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm rounded-md"
							bind:value={formData.userId}
						>
							<option value="">Vælg bruger</option>
							{#each users as user}
								<option value={user.id}>{user.username} ({user.email})</option>
							{/each}
						</select>
					</div>
					{#if errors.userId}
						<p class="mt-2 text-sm text-red-600">{errors.userId}</p>
					{/if}
				</div>

				<!-- Room Selection -->
				<div>
					<label for="roomId" class="block text-sm font-medium text-gray-700">
						Værelse
					</label>
					<div class="mt-1 relative">
						<div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
							<Bed class="h-5 w-5 text-gray-400" />
						</div>
						<select
							id="roomId"
							name="roomId"
							class="block w-full pl-10 pr-10 py-2 text-base border-gray-300 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm rounded-md"
							bind:value={formData.roomId}
						>
							<option value="">Vælg værelse</option>
							{#each rooms as room}
								<option value={room.id}>
									Værelse {room.number} - {room.hotel?.name || 'Ukendt hotel'} (Kapacitet: {room.capacity})
								</option>
							{/each}
						</select>
					</div>
					{#if errors.roomId}
						<p class="mt-2 text-sm text-red-600">{errors.roomId}</p>
					{/if}
				</div>

				<!-- Start Date -->
				<div>
					<label for="startDate" class="block text-sm font-medium text-gray-700">
						Start Dato
					</label>
					<div class="mt-1 relative">
						<div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
							<Calendar class="h-5 w-5 text-gray-400" />
						</div>
						<input
							type="date"
							name="startDate"
							id="startDate"
							class="block w-full pl-10 pr-3 py-2 border border-gray-300 rounded-md leading-5 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
							bind:value={formData.startDate}
						/>
					</div>
					{#if errors.dates}
						<p class="mt-2 text-sm text-red-600">{errors.dates}</p>
					{/if}
				</div>

				<!-- End Date -->
				<div>
					<label for="endDate" class="block text-sm font-medium text-gray-700">
						Slut Dato
					</label>
					<div class="mt-1 relative">
						<div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
							<Calendar class="h-5 w-5 text-gray-400" />
						</div>
						<input
							type="date"
							name="endDate"
							id="endDate"
							class="block w-full pl-10 pr-3 py-2 border border-gray-300 rounded-md leading-5 bg-white focus:outline-none focus:ring-1 focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
							bind:value={formData.endDate}
						/>
					</div>
				</div>
			</div>

			<!-- Actions -->
			<div class="flex justify-end space-x-3">
				<button
					type="button"
					class="bg-white py-2 px-4 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500"
					on:click={() => goto('/admin/bookings')}
				>
					Annuller
				</button>
				<button
					type="submit"
					disabled={isSubmitting}
					class="inline-flex justify-center py-2 px-4 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
				>
					{#if isSubmitting}
						<svg class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
							<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
							<path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
						</svg>
						Opretter...
					{:else}
						<Save class="h-4 w-4 mr-2" />
						Opret Booking
					{/if}
				</button>
			</div>
		</form>
	</div>
</div>
